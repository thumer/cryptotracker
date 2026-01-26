using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using CryptoTracker.Import.Objects;
using CryptoTracker.Shared;
using ExcelDataReader;
using CryptoTracker;

namespace CryptoTracker.Services;

public class ImportAutoService
{
    private const int PreviewLimit = 50;
    private static readonly CultureInfo CultureDe = new("de-AT");
    private static readonly CultureInfo CultureEn = new("en-US");
    private static readonly HashSet<string> BinanceStatementDepositOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "deposit",
        "asset - transfer",
        "staking rewards",
        "eth 2.0 staking rewards",
        "eth 2.0 staking rewards distribution",
        "staking distribution",
        "distribution",
        "rewards distribution",
        "launchpool rewards",
        "airdrop",
        "airdrop assets",
        "savings interest",
        "flexible savings interest",
        "locked savings interest",
        "earn interest",
        "interest",
        "mining rewards",
        "mining income",
        "token swap - distribution"
    };
    private static readonly HashSet<string> BinanceStatementWithdrawalOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "withdraw",
        "withdrawal",
        "transaction spend",
        "transaction fee",
        "fee",
        "token swap - redenomination/rebranding"
    };
    private static readonly string[] BinanceQuoteAssets =
    {
        "USDT", "FDUSD", "USDC", "BUSD", "TUSD", "USDP", "DAI", "PAX",
        "EUR", "GBP", "TRY", "AUD", "BRL", "RUB", "UAH", "NGN", "ZAR", "IDR", "BIDR",
        "JPY", "CHF", "CAD", "NZD", "PLN", "MXN",
        "BTC", "ETH", "BNB"
    };

    private readonly DataImportService _dataImportService;
    private readonly CoinRateService? _coinRateService;

    public ImportAutoService(DataImportService dataImportService, CoinRateService coinRateService)
    {
        _dataImportService = dataImportService;
        _coinRateService = coinRateService;
    }

    public ImportAutoService(DataImportService dataImportService)
    {
        _dataImportService = dataImportService;
    }

    public async Task<ImportPreviewResult> PreviewAsync(Func<Stream> openStream, string fileName)
    {
        try
        {
            var detected = Detect(openStream, fileName);
            var preview = BuildPreview(openStream, fileName, detected);
            return await ApplyPreviewSlugsAsync(preview);
        }
        catch (Exception ex)
        {
            return new ImportPreviewResult(false, ex.Message, null, null, null,
                new List<ImportPreviewTransactionRowDTO>(), false,
                new List<ImportPreviewTradeRowDTO>(), false);
        }
    }

    public ImportPreviewResult Preview(Func<Stream> openStream, string fileName)
        => PreviewAsync(openStream, fileName).GetAwaiter().GetResult();

    public async Task ImportAsync(string walletName, Func<Stream> openStream, string fileName, ImportDocumentType? documentType)
    {
        var detected = Detect(openStream, fileName);
        await ImportDetectedAsync(walletName, openStream, fileName, detected, documentType);
    }

    private enum ImportFileKind
    {
        Csv,
        Excel
    }

    private enum ImportFileVariant
    {
        BinanceDeposit,
        BinanceWithdrawal,
        BinanceTrade,
        BinanceConvertHistory,
        BinanceAccountStatement,
        CexIoWithdrawals,
        BitcoinDeAccountStatement,
        BitcoinDeBuyHistory,
        BitcoinDeSellHistory,
        BitcoinDeDepositHistory,
        BitcoinDeWithdrawalHistory,
        BitpandaTransactions,
        MetamaskTransactions,
        MetamaskTrades,
        LedgerTransactions,
        OkxDeposit,
        OkxTrade
    }

    private sealed record DetectedFile(ImportFileVariant Variant, ImportFileKind Kind, string? Delimiter, int HeaderRowIndex);
    private sealed record BinanceStatementRow(DateTimeOffset Date, string Operation, string Coin, decimal Change, string Remark);

    private DetectedFile Detect(Func<Stream> openStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension == ".xlsx" || extension == ".xls")
        {
            return DetectFromExcel(openStream, fileName);
        }

        return DetectFromCsv(openStream, fileName);
    }

    private DetectedFile DetectFromCsv(Func<Stream> openStream, string fileName)
    {
        var lines = ReadSampleLines(openStream, 80);
        if (lines.Count == 0)
        {
            throw new InvalidOperationException("Datei ist leer.");
        }

        var delimiters = new[] { ';', ',', '\t' };
        foreach (var delimiter in delimiters)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                var tokens = ParseCsvLine(lines[i], delimiter);
                var normalized = NormalizeHeaders(tokens);
                if (TryMatchVariant(normalized, fileName, out var variant))
                {
                    return new DetectedFile(variant, ImportFileKind.Csv, delimiter.ToString(), i);
                }
            }
        }

        throw new InvalidOperationException("Unbekanntes Import-Format. Bitte prüfe die Datei.");
    }

    private DetectedFile DetectFromExcel(Func<Stream> openStream, string fileName)
    {
        var rows = ReadExcelSampleRows(openStream, 80);
        if (rows.Count == 0)
        {
            throw new InvalidOperationException("Datei ist leer.");
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var normalized = NormalizeHeaders(rows[i]);
            if (TryMatchVariant(normalized, fileName, out var variant))
            {
                return new DetectedFile(variant, ImportFileKind.Excel, null, i);
            }
        }

        throw new InvalidOperationException("Unbekanntes Import-Format. Bitte prüfe die Datei.");
    }

    private ImportPreviewResult BuildPreview(Func<Stream> openStream, string fileName, DetectedFile detected)
    {
        var transactions = new List<ImportPreviewTransactionRowDTO>();
        var trades = new List<ImportPreviewTradeRowDTO>();
        var transactionsTruncated = false;
        var tradesTruncated = false;

        void AddTransaction(ImportPreviewTransactionRowDTO row)
        {
            if (transactions.Count < PreviewLimit)
            {
                transactions.Add(row);
            }
            else
            {
                transactionsTruncated = true;
            }
        }

        void AddTrade(ImportPreviewTradeRowDTO row)
        {
            if (trades.Count < PreviewLimit)
            {
                trades.Add(row);
            }
            else
            {
                tradesTruncated = true;
            }
        }

        if (detected.Variant == ImportFileVariant.BinanceAccountStatement)
        {
            var statementRows = EnumerateRows(openStream, detected)
                .Select(r => ParseBinanceStatementRowData(r, detected.Kind == ImportFileKind.Excel))
                .Where(r => r != null)
                .Select(r => r!)
                .ToList();

            foreach (var group in GroupStatementRows(statementRows))
            {
                if (transactionsTruncated && tradesTruncated)
                {
                    break;
                }

                var trade = BuildTradeFromStatementGroup(group);
                if (trade != null)
                {
                    AddTrade(BuildTradePreviewFromTrade(trade, "Binance"));
                    continue;
                }

                foreach (var entry in group)
                {
                    if (transactionsTruncated)
                    {
                        break;
                    }

                    var isDeposit = IsBinanceStatementDeposit(entry.Operation, entry.Change);
                    AddTransaction(BuildStatementTransactionPreview(entry, isDeposit, "Binance"));
                }
            }

            var documentTypeLocal = GetDocumentType(detected.Variant);
            var displayNameLocal = documentTypeLocal.HasValue ? documentTypeLocal.Value.GetDisplayName() : null;

            return new ImportPreviewResult(true, null, GetSourceLabel(detected.Variant), documentTypeLocal, displayNameLocal,
                transactions, transactionsTruncated, trades, tradesTruncated);
        }

        foreach (var row in EnumerateRows(openStream, detected))
        {
            if (transactionsTruncated && tradesTruncated)
            {
                break;
            }

            switch (detected.Variant)
            {
                case ImportFileVariant.BinanceDeposit:
                case ImportFileVariant.BinanceWithdrawal:
                    AddTransaction(BuildBinanceTransactionPreview(row,
                        NormalizeType(detected.Variant == ImportFileVariant.BinanceDeposit ? "Einzahlung" : "Auszahlung"),
                        GetValue(row, "coin"),
                        GetValue(row, "network"),
                        GetValue(row, "amount"),
                        GetValue(row, "transactionfee", "fee"),
                        GetValue(row, "address"),
                        GetValue(row, "comment", "remark", "status"),
                        detected.Kind == ImportFileKind.Excel));
                    break;
                case ImportFileVariant.BinanceTrade:
                    AddTrade(BuildBinanceTradePreview(row, detected.Kind == ImportFileKind.Excel));
                    break;
                case ImportFileVariant.BinanceConvertHistory:
                    AddTrade(BuildBinanceConvertPreview(row));
                    break;
                case ImportFileVariant.CexIoWithdrawals:
                    {
                        var address = GetValue(row, "walletaddress");
                        var coin = InferCoinFromAddress(address);
                        AddTransaction(BuildTransactionPreview(row, "Auszahlung", coin, null, GetValue(row, "amount"), "0",
                            address, GetValue(row, "comment"), "CEX.IO"));
                        break;
                    }
                case ImportFileVariant.BitcoinDeAccountStatement:
                    {
                        var typ = GetValue(row, "typ");
                        if (string.Equals(typ, "Kauf", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(typ, "Verkauf", StringComparison.OrdinalIgnoreCase))
                        {
                            AddTrade(BuildTradePreview(row, GetValue(row, "waehrung", "wahrung"), typ ?? string.Empty,
                                GetValue(row, "kurs"), GetValueBySuffix(row, "vorgebuhr"), GetValue(row, "mengenachbitcoindegebuhr", "mengenachgebuhr"), string.Empty, "Bitcoin.de"));
                        }
                        else if (string.Equals(typ, "Einzahlung", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(typ, "Auszahlung", StringComparison.OrdinalIgnoreCase))
                        {
                            AddTransaction(BuildTransactionPreview(row, NormalizeType(typ),
                                GetValue(row, "waehrung", "wahrung"), null, GetValue(row, "zuabgang"), "0",
                                GetValueBySuffix(row, "adresse"), GetValue(row, "kommentar"), "Bitcoin.de"));
                        }
                        else
                        {
                            var amountRaw = GetValue(row, "zuabgang");
                            var amount = ParseDecimalDe(amountRaw);
                            if (amount.HasValue && amount.Value != 0m)
                            {
                                var fallbackType = amount.Value < 0 ? "Auszahlung" : "Einzahlung";
                                var displayType = string.IsNullOrWhiteSpace(typ) ? fallbackType : typ;
                                AddTransaction(BuildTransactionPreview(row, displayType,
                                    GetValue(row, "waehrung", "wahrung"), null, amountRaw ?? string.Empty, "0",
                                    GetValueBySuffix(row, "adresse"), GetValue(row, "kommentar"), "Bitcoin.de"));
                            }
                        }
                        break;
                    }
                case ImportFileVariant.BitcoinDeBuyHistory:
                case ImportFileVariant.BitcoinDeSellHistory:
                    {
                        var typ = detected.Variant == ImportFileVariant.BitcoinDeBuyHistory ? "Kauf" : "Verkauf";
                        AddTrade(BuildTradePreview(row, GetValue(row, "symbol"), typ, GetValue(row, "kurs"),
                            GetValue(row, "menge"), GetValue(row, "betrag"), string.Empty, "Bitcoin.de"));
                        break;
                    }
                case ImportFileVariant.BitcoinDeDepositHistory:
                    AddTransaction(BuildTransactionPreview(row, "Einzahlung", GetValue(row, "symbol"), null, GetValue(row, "menge"), "0", null, null, "Bitcoin.de"));
                    break;
                case ImportFileVariant.BitcoinDeWithdrawalHistory:
                    AddTransaction(BuildTransactionPreview(row, "Auszahlung", GetValue(row, "symbol"), null, GetValue(row, "menge"), GetValue(row, "netzwerkgebuhr"), GetValue(row, "adresse"), GetValue(row, "kommentar"), "Bitcoin.de"));
                    break;
                case ImportFileVariant.BitpandaTransactions:
                    {
                        var type = GetValue(row, "transactiontype") ?? string.Empty;
                        if (string.Equals(type, "buy", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(type, "sell", StringComparison.OrdinalIgnoreCase))
                        {
                            AddTrade(BuildTradePreview(row, GetValue(row, "asset"), type, GetValue(row, "assetmarketprice"),
                                GetValue(row, "amountasset"), GetValue(row, "amountfiat"), GetValue(row, "fee"), "Bitpanda"));
                        }
                        else if (string.Equals(type, "deposit", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(type, "withdrawal", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(type, "transfer", StringComparison.OrdinalIgnoreCase))
                        {
                            var inOut = GetValue(row, "inout");
                            var normalizedType = string.Equals(type, "transfer", StringComparison.OrdinalIgnoreCase)
                                ? NormalizeTransferType(inOut)
                                : NormalizeType(type);

                            AddTransaction(BuildTransactionPreview(row, normalizedType, GetValue(row, "asset"), null,
                                GetValue(row, "amountasset"), GetValue(row, "fee"), GetValue(row, "address"), GetValue(row, "comment"), "Bitpanda"));
                        }
                        break;
                    }
                case ImportFileVariant.MetamaskTransactions:
                    AddTransaction(BuildTransactionPreview(row, NormalizeType(GetValue(row, "typ")), GetValue(row, "coin"), GetValue(row, "network"),
                        GetValue(row, "amount"), GetValue(row, "transactionfee"), null, GetValue(row, "kommentar"), "Metamask"));
                    break;
                case ImportFileVariant.LedgerTransactions:
                    AddTransaction(BuildTransactionPreview(row, NormalizeType(GetValue(row, "typ")), GetValue(row, "coin"), GetValue(row, "network"),
                        GetValue(row, "amount"), GetValue(row, "transactionfee"), GetValue(row, "address"), GetValue(row, "kommentar"), "Ledger"));
                    break;
                case ImportFileVariant.MetamaskTrades:
                    AddTrade(BuildTradePreview(row, GetValue(row, "pair"), GetValue(row, "side"), GetValue(row, "price"), GetValue(row, "executed"),
                        GetValue(row, "amount"), GetValue(row, "fee"), GetValue(row, "tradingplatform") ?? "Metamask"));
                    break;
                case ImportFileVariant.OkxDeposit:
                    AddTransaction(BuildTransactionPreview(row, "Einzahlung", GetValue(row, "coin"), GetValue(row, "network"), GetValue(row, "amount"), "0",
                        GetValue(row, "address"), GetValue(row, "kommentar", "comment"), "OKX"));
                    break;
                case ImportFileVariant.OkxTrade:
                    AddTrade(BuildTradePreview(row, GetValue(row, "pair"), GetValue(row, "side"), GetValue(row, "price"), GetValue(row, "executed"),
                        string.Empty, string.Empty, "OKX"));
                    break;
            }
        }

        var documentType = GetDocumentType(detected.Variant);
        var displayName = documentType.HasValue ? documentType.Value.GetDisplayName() : null;

        return new ImportPreviewResult(true, null, GetSourceLabel(detected.Variant), documentType, displayName,
            transactions, transactionsTruncated, trades, tradesTruncated);
    }

    private async Task<ImportPreviewResult> ApplyPreviewSlugsAsync(ImportPreviewResult preview)
    {
        if (_coinRateService == null || !preview.Success || preview.TransactionRows.Count == 0)
        {
            return preview;
        }

        var symbols = preview.TransactionRows
            .Select(r => r.Coin.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (symbols.Count == 0)
        {
            return preview;
        }

        var slugs = await _coinRateService.GetSlugsAsync(symbols);
        var updated = preview.TransactionRows
            .Select(row =>
            {
                var symbol = row.Coin.Trim();
                var slug = !string.IsNullOrWhiteSpace(symbol) && slugs.TryGetValue(symbol, out var value)
                    ? value
                    : null;
                return row with { Slug = slug };
            })
            .ToList();

        return preview with { TransactionRows = updated };
    }

    private async Task ImportDetectedAsync(string walletName, Func<Stream> openStream, string fileName, DetectedFile detected, ImportDocumentType? documentType)
    {
        switch (detected.Variant)
        {
            case ImportFileVariant.BinanceDeposit:
                await ImportCsvAsync<BinanceDeposit>(walletName, openStream, detected, ImportDocumentType.BinanceDepositHistory,
                    row => ParseBinanceDepositRow(row, detected.Kind == ImportFileKind.Excel));
                break;
            case ImportFileVariant.BinanceWithdrawal:
                await ImportCsvAsync<BinanceWithdrawal>(walletName, openStream, detected, ImportDocumentType.BinanceWithdrawalHistory,
                    row => ParseBinanceWithdrawalRow(row, detected.Kind == ImportFileKind.Excel));
                break;
            case ImportFileVariant.BinanceTrade:
                await ImportCsvAsync<BinanceTrade>(walletName, openStream, detected, ImportDocumentType.BinanceTradingHistory,
                    row => ParseBinanceTradeRow(row, detected.Kind == ImportFileKind.Excel));
                break;
            case ImportFileVariant.BinanceConvertHistory:
                await ImportCsvAsync<BinanceTrade>(walletName, openStream, detected, ImportDocumentType.BinanceTradingHistory, ParseBinanceConvertRow);
                break;
            case ImportFileVariant.BinanceAccountStatement:
                await ImportBinanceAccountStatementAsync(walletName, openStream, detected);
                break;
            case ImportFileVariant.CexIoWithdrawals:
                await ImportCsvAsync<BinanceWithdrawal>(walletName, openStream, detected, ImportDocumentType.BinanceWithdrawalHistory, ParseCexIoWithdrawalRow);
                break;
            case ImportFileVariant.BitcoinDeAccountStatement:
                await ImportCsvAsync<BitcoinDeTransaction>(walletName, openStream, detected, ImportDocumentType.BitcoinDeTransactions, ParseBitcoinDeAccountRow);
                break;
            case ImportFileVariant.BitcoinDeBuyHistory:
                await ImportCsvAsync<BitcoinDeTransaction>(walletName, openStream, detected, ImportDocumentType.BitcoinDeTransactions,
                    row => ParseBitcoinDeBuySellRow(row, "Kauf"));
                break;
            case ImportFileVariant.BitcoinDeSellHistory:
                await ImportCsvAsync<BitcoinDeTransaction>(walletName, openStream, detected, ImportDocumentType.BitcoinDeTransactions,
                    row => ParseBitcoinDeBuySellRow(row, "Verkauf"));
                break;
            case ImportFileVariant.BitcoinDeDepositHistory:
                await ImportCsvAsync<BitcoinDeTransaction>(walletName, openStream, detected, ImportDocumentType.BitcoinDeTransactions,
                    ParseBitcoinDeDepositRow);
                break;
            case ImportFileVariant.BitcoinDeWithdrawalHistory:
                await ImportCsvAsync<BitcoinDeTransaction>(walletName, openStream, detected, ImportDocumentType.BitcoinDeTransactions,
                    ParseBitcoinDeWithdrawalRow);
                break;
            case ImportFileVariant.BitpandaTransactions:
                await ImportCsvAsync<BitpandaTransaction>(walletName, openStream, detected, ImportDocumentType.BitpandaTransaction, ParseBitpandaRow);
                break;
            case ImportFileVariant.MetamaskTransactions:
                await ImportCsvAsync<MetamaskTransaction>(walletName, openStream, detected, ImportDocumentType.MetamaskTransactions, ParseMetamaskTransactionRow);
                break;
            case ImportFileVariant.LedgerTransactions:
                await ImportCsvAsync<LedgerTransaction>(walletName, openStream, detected, ImportDocumentType.LedgerTransactions, ParseLedgerTransactionRow);
                break;
            case ImportFileVariant.MetamaskTrades:
                await ImportCsvAsync<MetamaskTrade>(walletName, openStream, detected, ImportDocumentType.MetamaskTradingHistory, ParseMetamaskTradeRow);
                break;
            case ImportFileVariant.OkxDeposit:
                await ImportCsvAsync<OkxDeposit>(walletName, openStream, detected, ImportDocumentType.OkxDepositHistory, ParseOkxDepositRow);
                break;
            case ImportFileVariant.OkxTrade:
                await ImportCsvAsync<OkxTrade>(walletName, openStream, detected, ImportDocumentType.OkxTradingHistory, ParseOkxTradeRow);
                break;
            default:
                throw new InvalidOperationException("Import-Format wird noch nicht unterstützt.");
        }
    }

    private async Task ImportBinanceAccountStatementAsync(string walletName, Func<Stream> openStream, DetectedFile detected)
    {
        var deposits = new List<BinanceDeposit>();
        var withdrawals = new List<BinanceWithdrawal>();
        var trades = new List<BinanceTrade>();

        var rows = EnumerateRows(openStream, detected)
            .Select(r => ParseBinanceStatementRowData(r, detected.Kind == ImportFileKind.Excel))
            .Where(r => r != null)
            .Select(r => r!)
            .ToList();

        foreach (var group in GroupStatementRows(rows))
        {
            var trade = BuildTradeFromStatementGroup(group);
            if (trade != null)
            {
                trades.Add(trade);
                continue;
            }

            foreach (var entry in group)
            {
                var isDeposit = IsBinanceStatementDeposit(entry.Operation, entry.Change);
                var amount = Math.Abs(entry.Change);
                var comment = BuildOperationComment(entry.Operation, entry.Remark);
                var coin = string.IsNullOrWhiteSpace(entry.Coin) ? "UNKNOWN" : entry.Coin;
                var date = entry.Date;

                if (isDeposit)
                {
                    var txid = EnsureTxId(null, "binance-statement-deposit", date.ToString("O"), coin, amount.ToString(CultureEn), entry.Operation, comment);
                    deposits.Add(new BinanceDeposit
                    {
                        Date = date,
                        Coin = coin,
                        Network = string.Empty,
                        Amount = amount,
                        TransactionFee = 0m,
                        Address = string.Empty,
                        TXID = txid,
                        Comment = comment
                    });
                }
                else
                {
                    var txid = EnsureTxId(null, "binance-statement-withdrawal", date.ToString("O"), coin, amount.ToString(CultureEn), entry.Operation, comment);
                    withdrawals.Add(new BinanceWithdrawal
                    {
                        Date = date,
                        Coin = coin,
                        Network = string.Empty,
                        Amount = amount,
                        TransactionFee = 0m,
                        Address = string.Empty,
                        TXID = txid,
                        Comment = comment
                    });
                }
            }
        }

        if (deposits.Count == 0 && withdrawals.Count == 0 && trades.Count == 0)
        {
            throw new InvalidOperationException("Keine Ein- oder Auszahlungen im Statement gefunden.");
        }

        if (deposits.Count > 0)
        {
            await ImportRecordsAsync(walletName, ImportDocumentType.BinanceDepositHistory, deposits);
        }

        if (withdrawals.Count > 0)
        {
            await ImportRecordsAsync(walletName, ImportDocumentType.BinanceWithdrawalHistory, withdrawals);
        }

        if (trades.Count > 0)
        {
            await ImportRecordsAsync(walletName, ImportDocumentType.BinanceTradingHistory, trades);
        }
    }

    private async Task ImportCsvAsync<T>(string walletName,
        Func<Stream> openStream,
        DetectedFile detected,
        ImportDocumentType documentType,
        Func<Dictionary<string, string?>, T?> parser)
        where T : class
    {
        var records = new List<T>();
        foreach (var row in EnumerateRows(openStream, detected))
        {
            var record = parser(row);
            if (record != null)
            {
                records.Add(record);
            }
        }

        if (records.Count == 0)
        {
            throw new InvalidOperationException("Keine importierbaren Zeilen gefunden.");
        }

        await ImportRecordsAsync(walletName, documentType, records);
    }

    private async Task ImportRecordsAsync<T>(string walletName, ImportDocumentType documentType, IList<T> records)
        where T : class
    {
        var csvBytes = BuildCsvBytes(documentType, records);
        await _dataImportService.Import(documentType, walletName, () => new MemoryStream(csvBytes));
    }

    private static byte[] BuildCsvBytes<T>(ImportDocumentType documentType, IList<T> records)
        where T : class
    {
        var config = CreateCsvConfiguration(documentType);
        using var memory = new MemoryStream();
        using var writer = new StreamWriter(memory, new UTF8Encoding(true), leaveOpen: true);
        using var csv = new CsvWriter(writer, config);
        csv.WriteHeader<T>();
        csv.NextRecord();
        foreach (var record in records)
        {
            csv.WriteRecord(record);
            csv.NextRecord();
        }

        writer.Flush();
        memory.Position = 0;
        return memory.ToArray();
    }

    private static CsvConfiguration CreateCsvConfiguration(ImportDocumentType type)
    {
        return type switch
        {
            ImportDocumentType.BinanceDepositHistory or
            ImportDocumentType.BinanceWithdrawalHistory or
            ImportDocumentType.OkxDepositHistory or
            ImportDocumentType.OkxTradingHistory or
            ImportDocumentType.MetamaskTradingHistory or
            ImportDocumentType.MetamaskTransactions or
            ImportDocumentType.LedgerTransactions =>
                new CsvConfiguration(CultureDe) { Delimiter = ";" },
            ImportDocumentType.BinanceTradingHistory =>
                new CsvConfiguration(CultureEn),
            ImportDocumentType.BitcoinDeTransactions =>
                new CsvConfiguration(CultureEn) { Delimiter = ";" },
            ImportDocumentType.BitpandaTransaction =>
                new CsvConfiguration(CultureEn),
            _ => new CsvConfiguration(CultureInfo.InvariantCulture)
        };
    }

    private static List<string> ReadSampleLines(Func<Stream> openStream, int maxLines)
    {
        using var stream = openStream();
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        var lines = new List<string>();
        while (lines.Count < maxLines)
        {
            var line = reader.ReadLine();
            if (line == null)
            {
                break;
            }
            lines.Add(line);
        }

        return lines;
    }

    private static List<string[]> ReadExcelSampleRows(Func<Stream> openStream, int maxRows)
    {
        using var stream = openStream();
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var rows = new List<string[]>();
        do
        {
            while (reader.Read())
            {
                var values = new string[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    values[i] = ConvertCellValue(reader.GetValue(i));
                }
                rows.Add(values);
                if (rows.Count >= maxRows)
                {
                    return rows;
                }
            }
        } while (reader.NextResult());

        return rows;
    }

    private static IEnumerable<Dictionary<string, string?>> EnumerateRows(Func<Stream> openStream, DetectedFile detected)
    {
        return detected.Kind switch
        {
            ImportFileKind.Csv => EnumerateCsvRows(openStream, detected.Delimiter ?? ";", detected.HeaderRowIndex),
            ImportFileKind.Excel => EnumerateExcelRows(openStream, detected.HeaderRowIndex),
            _ => Enumerable.Empty<Dictionary<string, string?>>()
        };
    }

    private static IEnumerable<Dictionary<string, string?>> EnumerateCsvRows(Func<Stream> openStream, string delimiter, int headerRowIndex)
    {
        using var stream = openStream();
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);

        for (var i = 0; i < headerRowIndex; i++)
        {
            if (reader.ReadLine() == null)
            {
                yield break;
            }
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = true,
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim
        };

        using var csv = new CsvReader(reader, config);
        if (!csv.Read())
        {
            yield break;
        }
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        var normalizedHeaders = headers.Select(NormalizeHeader).ToArray();

        while (csv.Read())
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < normalizedHeaders.Length; i++)
            {
                var key = normalizedHeaders[i];
                if (string.IsNullOrWhiteSpace(key) || row.ContainsKey(key))
                {
                    continue;
                }

                row[key] = csv.GetField(i);
            }

            if (row.Count > 0)
            {
                yield return row;
            }
        }
    }

    private static IEnumerable<Dictionary<string, string?>> EnumerateExcelRows(Func<Stream> openStream, int headerRowIndex)
    {
        using var stream = openStream();
        using var reader = ExcelReaderFactory.CreateReader(stream);
        do
        {
            var rowIndex = 0;
            string[]? headers = null;
            while (reader.Read())
            {
                if (rowIndex == headerRowIndex)
                {
                    headers = new string[reader.FieldCount];
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        headers[i] = ConvertCellValue(reader.GetValue(i));
                    }
                    headers = headers.Select(NormalizeHeader).ToArray();
                    rowIndex++;
                    continue;
                }

                if (rowIndex > headerRowIndex && headers != null)
                {
                    var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < reader.FieldCount && i < headers.Length; i++)
                    {
                        var key = headers[i];
                        if (string.IsNullOrWhiteSpace(key) || row.ContainsKey(key))
                        {
                            continue;
                        }

                        row[key] = ConvertCellValue(reader.GetValue(i));
                    }

                    if (row.Count > 0)
                    {
                        yield return row;
                    }
                }

                rowIndex++;
            }

            if (headers != null)
            {
                yield break;
            }
        } while (reader.NextResult());
    }

    private static string ConvertCellValue(object? value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        return value switch
        {
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            decimal dec => dec.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string[] ParseCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        if (line.Length == 0)
        {
            return Array.Empty<string>();
        }

        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '\"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                {
                    sb.Append('\"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (!inQuotes && ch == delimiter)
            {
                result.Add(sb.ToString());
                sb.Clear();
                continue;
            }

            sb.Append(ch);
        }

        result.Add(sb.ToString());
        return result.ToArray();
    }

    private static IReadOnlyList<string> NormalizeHeaders(IReadOnlyList<string> tokens)
        => tokens.Select(NormalizeHeader).ToList();

    private static string NormalizeHeader(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return string.Empty;
        }

        var trimmed = header.Trim().Trim('"').Replace("\uFEFF", string.Empty);
        var normalized = trimmed.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
        }

        return sb.ToString();
    }

    private static bool TryMatchVariant(IReadOnlyList<string> headers, string fileName, out ImportFileVariant variant)
    {
        variant = default;
        var set = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);

        if (set.Contains("transactionid") && set.Contains("transactiontype") && set.Contains("amountfiat"))
        {
            variant = ImportFileVariant.BitpandaTransactions;
            return true;
        }

        if (set.Contains("pair") && set.Contains("sell") && set.Contains("buy") && (set.Contains("dateupdated") || set.Contains("price") || set.Contains("inverseprice")))
        {
            variant = ImportFileVariant.BinanceConvertHistory;
            return true;
        }

        if (set.Contains("userid") && set.Contains("operation") && set.Contains("change") &&
            (set.Contains("utctime") || set.Contains("time") || set.Contains("date")))
        {
            variant = ImportFileVariant.BinanceAccountStatement;
            return true;
        }

        if (set.Contains("date") && set.Contains("amount") && set.Contains("walletaddress"))
        {
            variant = ImportFileVariant.CexIoWithdrawals;
            return true;
        }

        if (set.Contains("tradingplatform") && set.Contains("pair") && set.Contains("side"))
        {
            variant = ImportFileVariant.MetamaskTrades;
            return true;
        }

        if (set.Contains("dateutc") && set.Contains("price") &&
            ((set.Contains("pair") && set.Contains("side")) || (set.Contains("market") && set.Contains("type"))))
        {
            if (set.Contains("amount") && set.Contains("fee"))
            {
                variant = ImportFileVariant.BinanceTrade;
                return true;
            }

            variant = ImportFileVariant.OkxTrade;
            return true;
        }

        if (set.Contains("dateutc") && set.Contains("coin") && set.Contains("amount"))
        {
            if (set.Contains("transactionfee") || set.Contains("txid"))
            {
                var lower = fileName.ToLowerInvariant();
                if (lower.Contains("withd") || lower.Contains("withdraw"))
                {
                    variant = ImportFileVariant.BinanceWithdrawal;
                }
                else
                {
                    variant = ImportFileVariant.BinanceDeposit;
                }

                return true;
            }

            variant = ImportFileVariant.OkxDeposit;
            return true;
        }

        if (set.Contains("time") && set.Contains("coin") && set.Contains("amount"))
        {
            var lower = fileName.ToLowerInvariant();
            if (set.Contains("fee") || lower.Contains("withd") || lower.Contains("withdraw"))
            {
                variant = ImportFileVariant.BinanceWithdrawal;
            }
            else
            {
                variant = ImportFileVariant.BinanceDeposit;
            }

            return true;
        }

        if ((set.Contains("datumutc") || set.Contains("datum")) &&
            set.Contains("typ") && set.Contains("coin") && set.Contains("transactionfee") && set.Contains("address"))
        {
            variant = ImportFileVariant.LedgerTransactions;
            return true;
        }

        if ((set.Contains("datumutc") || set.Contains("datum")) && set.Contains("typ") && set.Contains("coin") && set.Contains("transactionfee"))
        {
            variant = ImportFileVariant.MetamaskTransactions;
            return true;
        }

        if (set.Contains("datum") && set.Contains("typ") && (set.Contains("waehrung") || set.Contains("wahrung")) && set.Contains("zuabgang"))
        {
            variant = ImportFileVariant.BitcoinDeAccountStatement;
            return true;
        }

        if (set.Contains("datum") && set.Contains("symbol") && set.Contains("kurs") && set.Contains("menge") && set.Contains("betrag"))
        {
            var lower = fileName.ToLowerInvariant();
            if (lower.Contains("sell"))
            {
                variant = ImportFileVariant.BitcoinDeSellHistory;
            }
            else
            {
                variant = ImportFileVariant.BitcoinDeBuyHistory;
            }

            return true;
        }

        if (set.Contains("datum") && set.Contains("symbol") && set.Contains("menge") && set.Contains("adresse"))
        {
            variant = ImportFileVariant.BitcoinDeWithdrawalHistory;
            return true;
        }

        if (set.Contains("datum") && set.Contains("symbol") && set.Contains("menge"))
        {
            variant = ImportFileVariant.BitcoinDeDepositHistory;
            return true;
        }

        return false;
    }

    private static ImportDocumentType? GetDocumentType(ImportFileVariant variant)
    {
        return variant switch
        {
            ImportFileVariant.BinanceDeposit => ImportDocumentType.BinanceDepositHistory,
            ImportFileVariant.BinanceWithdrawal => ImportDocumentType.BinanceWithdrawalHistory,
            ImportFileVariant.BinanceTrade => ImportDocumentType.BinanceTradingHistory,
            ImportFileVariant.BinanceConvertHistory => ImportDocumentType.BinanceTradingHistory,
            ImportFileVariant.CexIoWithdrawals => ImportDocumentType.BinanceWithdrawalHistory,
            ImportFileVariant.BitcoinDeAccountStatement or
            ImportFileVariant.BitcoinDeBuyHistory or
            ImportFileVariant.BitcoinDeSellHistory or
            ImportFileVariant.BitcoinDeDepositHistory or
            ImportFileVariant.BitcoinDeWithdrawalHistory => ImportDocumentType.BitcoinDeTransactions,
            ImportFileVariant.BitpandaTransactions => ImportDocumentType.BitpandaTransaction,
            ImportFileVariant.MetamaskTransactions => ImportDocumentType.MetamaskTransactions,
            ImportFileVariant.MetamaskTrades => ImportDocumentType.MetamaskTradingHistory,
            ImportFileVariant.LedgerTransactions => ImportDocumentType.LedgerTransactions,
            ImportFileVariant.OkxDeposit => ImportDocumentType.OkxDepositHistory,
            ImportFileVariant.OkxTrade => ImportDocumentType.OkxTradingHistory,
            _ => null
        };
    }

    private static string GetSourceLabel(ImportFileVariant variant)
    {
        return variant switch
        {
            ImportFileVariant.BinanceDeposit or ImportFileVariant.BinanceWithdrawal or ImportFileVariant.BinanceTrade or ImportFileVariant.BinanceConvertHistory or ImportFileVariant.BinanceAccountStatement => "Binance",
            ImportFileVariant.CexIoWithdrawals => "CEX.IO",
            ImportFileVariant.BitcoinDeAccountStatement or ImportFileVariant.BitcoinDeBuyHistory or ImportFileVariant.BitcoinDeSellHistory or ImportFileVariant.BitcoinDeDepositHistory or ImportFileVariant.BitcoinDeWithdrawalHistory => "Bitcoin.de",
            ImportFileVariant.BitpandaTransactions => "Bitpanda",
            ImportFileVariant.MetamaskTransactions or ImportFileVariant.MetamaskTrades => "Metamask",
            ImportFileVariant.LedgerTransactions => "Ledger",
            ImportFileVariant.OkxDeposit or ImportFileVariant.OkxTrade => "OKX",
            _ => "Import"
        };
    }

    private static ImportPreviewTransactionRowDTO BuildTransactionPreview(Dictionary<string, string?> row,
        string type,
        string? coin,
        string? network,
        string? amount,
        string? fee,
        string? address,
        string? comment,
        string? source)
    {
        var date = ParseDateTimeOffset(GetValue(row, "dateutc", "datumutc", "datum", "timestamp", "utctime", "date", "time"));
        return new ImportPreviewTransactionRowDTO(date, type, coin ?? string.Empty, null, network, amount ?? string.Empty,
            fee ?? string.Empty, address, comment, source);
    }

    private static ImportPreviewTradeRowDTO BuildTradePreview(Dictionary<string, string?> row,
        string? pair,
        string? side,
        string? price,
        string? executed,
        string? amount,
        string? fee,
        string? source)
    {
        var date = ParseDateTimeOffset(GetValue(row, "dateutc", "datum", "timestamp"));
        return new ImportPreviewTradeRowDTO(date, pair ?? string.Empty, side ?? string.Empty, price ?? string.Empty,
            executed ?? string.Empty, amount ?? string.Empty, fee ?? string.Empty, source);
    }

    private static ImportPreviewTradeRowDTO BuildBinanceTradePreview(Dictionary<string, string?> row, bool isExcel)
    {
        var hasUtcHeader = HasUtcHeader(row);
        var date = ParseBinanceExchangeTime(GetValue(row, "dateutc"), isExcel, hasUtcHeader);
        var pair = GetValue(row, "pair", "market") ?? string.Empty;
        var side = GetValue(row, "side", "type") ?? string.Empty;
        var price = GetValue(row, "price") ?? string.Empty;
        var executed = GetValue(row, "executed");
        var amount = GetValue(row, "amount");
        var fee = GetValue(row, "fee");
        var total = GetValue(row, "total");
        var feeCoin = GetValue(row, "feecoin");

        if (!string.IsNullOrWhiteSpace(total))
        {
            var (baseSymbol, quoteSymbol) = SplitBinancePair(pair);
            var baseAmount = ParseDecimal(amount) ?? 0m;
            var quoteAmount = ParseDecimal(total) ?? 0m;
            executed = $"{baseAmount.ToString(CultureEn)}{baseSymbol}";
            amount = $"{quoteAmount.ToString(CultureEn)}{quoteSymbol}";
            if (!string.IsNullOrWhiteSpace(feeCoin))
            {
                var feeAmount = ParseDecimal(fee) ?? 0m;
                fee = $"{feeAmount.ToString(CultureEn)}{feeCoin}";
            }
        }

        return new ImportPreviewTradeRowDTO(date, pair, side, price, executed ?? string.Empty, amount ?? string.Empty,
            fee ?? string.Empty, "Binance");
    }

    private static ImportPreviewTransactionRowDTO BuildBinanceTransactionPreview(Dictionary<string, string?> row,
        string type,
        string? coin,
        string? network,
        string? amount,
        string? fee,
        string? address,
        string? comment,
        bool isExcel)
    {
        var hasUtcHeader = HasUtcHeader(row);
        var date = ParseBinanceExchangeTime(GetValue(row, "dateutc", "time"), isExcel, hasUtcHeader);
        return new ImportPreviewTransactionRowDTO(date, type, coin ?? string.Empty, null, network, amount ?? string.Empty,
            fee ?? string.Empty, address, comment, "Binance");
    }

    private static ImportPreviewTradeRowDTO BuildTradePreviewFromTrade(BinanceTrade trade, string source)
    {
        return new ImportPreviewTradeRowDTO(trade.Date, trade.Pair, trade.Side, trade.Price.ToString(CultureEn),
            trade.Executed, trade.Amount, trade.Fee, source);
    }

    private static ImportPreviewTransactionRowDTO BuildStatementTransactionPreview(BinanceStatementRow row, bool isDeposit, string source)
    {
        var amountText = FormatDecimal(Math.Abs(row.Change));
        var comment = BuildOperationComment(row.Operation, row.Remark);
        return new ImportPreviewTransactionRowDTO(row.Date, isDeposit ? "Einzahlung" : "Auszahlung", row.Coin ?? "UNKNOWN",
            null, null, amountText, "0", null, comment, source);
    }

    private static ImportPreviewTradeRowDTO BuildBinanceConvertPreview(Dictionary<string, string?> row)
    {
        var date = ParseDateTimeOffset(GetValue(row, "dateupdated"));
        var pair = GetValue(row, "pair") ?? string.Empty;
        var price = GetValue(row, "price") ?? string.Empty;
        var convertTrade = ParseBinanceConvertRow(row);
        if (convertTrade == null)
        {
            return new ImportPreviewTradeRowDTO(date, pair, string.Empty, price, string.Empty, string.Empty, string.Empty, "Binance");
        }

        return new ImportPreviewTradeRowDTO(convertTrade.Date, convertTrade.Pair, convertTrade.Side, price,
            convertTrade.Executed, convertTrade.Amount, convertTrade.Fee, "Binance");
    }

    private static BinanceDeposit? ParseBinanceDepositRow(Dictionary<string, string?> row, bool isExcel)
    {
        var hasUtcHeader = HasUtcHeader(row);
        var date = ParseBinanceExchangeTime(GetValue(row, "dateutc", "time"), isExcel, hasUtcHeader);
        var coin = GetValue(row, "coin");
        if (string.IsNullOrWhiteSpace(coin))
        {
            return null;
        }

        var amount = ParseDecimal(GetValue(row, "amount")) ?? 0m;
        var address = GetValue(row, "address") ?? string.Empty;
        var comment = GetValue(row, "comment", "remark", "status") ?? string.Empty;
        var txid = EnsureTxId(GetValue(row, "txid"), "binance-deposit", date.ToString("O"), coin, amount.ToString(CultureEn), address, comment);

        return new BinanceDeposit
        {
            Date = date,
            Coin = coin,
            Network = GetValue(row, "network") ?? string.Empty,
            Amount = amount,
            TransactionFee = ParseDecimal(GetValue(row, "transactionfee", "fee")) ?? 0m,
            Address = address,
            TXID = txid,
            Comment = comment
        };
    }

    private static BinanceWithdrawal? ParseBinanceWithdrawalRow(Dictionary<string, string?> row, bool isExcel)
    {
        var hasUtcHeader = HasUtcHeader(row);
        var date = ParseBinanceExchangeTime(GetValue(row, "dateutc", "time"), isExcel, hasUtcHeader);
        var coin = GetValue(row, "coin");
        if (string.IsNullOrWhiteSpace(coin))
        {
            return null;
        }

        var amount = ParseDecimal(GetValue(row, "amount")) ?? 0m;
        var address = GetValue(row, "address") ?? string.Empty;
        var comment = GetValue(row, "comment", "remark", "status") ?? string.Empty;
        var txid = EnsureTxId(GetValue(row, "txid"), "binance-withdrawal", date.ToString("O"), coin, amount.ToString(CultureEn), address, comment);

        return new BinanceWithdrawal
        {
            Date = date,
            Coin = coin,
            Network = GetValue(row, "network") ?? string.Empty,
            Amount = amount,
            TransactionFee = ParseDecimal(GetValue(row, "transactionfee", "fee")) ?? 0m,
            Address = address,
            TXID = txid,
            Comment = comment
        };
    }

    private static BinanceWithdrawal? ParseCexIoWithdrawalRow(Dictionary<string, string?> row)
    {
        var date = ParseDateTimeOffset(GetValue(row, "date"));
        var amount = ParseDecimal(GetValue(row, "amount"));
        if (amount == null)
        {
            return null;
        }

        var address = GetValue(row, "walletaddress");
        var coin = InferCoinFromAddress(address);
        var comment = GetValue(row, "comment") ?? string.Empty;
        var txid = EnsureTxId(null, "cexio-withdrawal", date.ToString("O"), coin, amount.Value.ToString(CultureEn), address, comment);

        return new BinanceWithdrawal
        {
            Date = date,
            Coin = coin,
            Network = string.Empty,
            Amount = amount.Value,
            TransactionFee = 0m,
            Address = address ?? string.Empty,
            TXID = txid,
            Comment = comment
        };
    }

    private static BinanceTrade? ParseBinanceTradeRow(Dictionary<string, string?> row, bool isExcel)
    {
        var hasUtcHeader = HasUtcHeader(row);
        var date = ParseBinanceExchangeTime(GetValue(row, "dateutc"), isExcel, hasUtcHeader);
        var pair = GetValue(row, "pair", "market");
        if (string.IsNullOrWhiteSpace(pair))
        {
            return null;
        }

        var executed = GetValue(row, "executed");
        var amount = GetValue(row, "amount");
        var total = GetValue(row, "total");
        var fee = GetValue(row, "fee");
        var feeCoin = GetValue(row, "feecoin");

        if (!string.IsNullOrWhiteSpace(total))
        {
            var (baseSymbol, quoteSymbol) = SplitBinancePair(pair);
            var baseAmount = ParseDecimal(amount) ?? 0m;
            var quoteAmount = ParseDecimal(total) ?? 0m;
            executed = $"{baseAmount.ToString(CultureEn)}{baseSymbol}";
            amount = $"{quoteAmount.ToString(CultureEn)}{quoteSymbol}";
            if (!string.IsNullOrWhiteSpace(feeCoin))
            {
                var feeAmount = ParseDecimal(fee) ?? 0m;
                fee = $"{feeAmount.ToString(CultureEn)}{feeCoin}";
            }
        }
        else if (!string.IsNullOrWhiteSpace(feeCoin) && !string.IsNullOrWhiteSpace(fee) &&
                 !fee.Contains(feeCoin, StringComparison.OrdinalIgnoreCase))
        {
            var feeAmount = ParseDecimal(fee) ?? 0m;
            fee = $"{feeAmount.ToString(CultureEn)}{feeCoin}";
        }

        return new BinanceTrade
        {
            Date = date,
            Pair = pair,
            Side = GetValue(row, "side", "type") ?? string.Empty,
            Price = ParseDecimal(GetValue(row, "price")) ?? 0m,
            Executed = executed ?? string.Empty,
            Amount = amount ?? string.Empty,
            Fee = fee ?? string.Empty
        };
    }

    private static BinanceTrade? ParseBinanceConvertRow(Dictionary<string, string?> row)
    {
        var pair = GetValue(row, "pair");
        if (string.IsNullOrWhiteSpace(pair))
        {
            return null;
        }

        var (sellAmount, sellSymbol) = ParseAmountWithSymbol(GetValue(row, "sell"));
        var (buyAmount, buySymbol) = ParseAmountWithSymbol(GetValue(row, "buy"));

        string side;
        string baseSymbol;
        string quoteSymbol;
        decimal baseAmount;
        decimal quoteAmount;

        if (!string.IsNullOrWhiteSpace(buySymbol) && pair.StartsWith(buySymbol, StringComparison.OrdinalIgnoreCase))
        {
            side = "BUY";
            baseSymbol = buySymbol;
            quoteSymbol = pair.Substring(buySymbol.Length);
            baseAmount = buyAmount;
            quoteAmount = sellAmount;
        }
        else if (!string.IsNullOrWhiteSpace(sellSymbol) && pair.StartsWith(sellSymbol, StringComparison.OrdinalIgnoreCase))
        {
            side = "SELL";
            baseSymbol = sellSymbol;
            quoteSymbol = pair.Substring(sellSymbol.Length);
            baseAmount = sellAmount;
            quoteAmount = buyAmount;
        }
        else if (!string.IsNullOrWhiteSpace(buySymbol) && pair.EndsWith(buySymbol, StringComparison.OrdinalIgnoreCase))
        {
            quoteSymbol = buySymbol;
            baseSymbol = pair.Substring(0, pair.Length - buySymbol.Length);
            var sellIsBase = string.Equals(sellSymbol, baseSymbol, StringComparison.OrdinalIgnoreCase);
            side = sellIsBase ? "SELL" : "BUY";
            baseAmount = sellIsBase ? sellAmount : buyAmount;
            quoteAmount = sellIsBase ? buyAmount : sellAmount;
        }
        else if (!string.IsNullOrWhiteSpace(sellSymbol) && pair.EndsWith(sellSymbol, StringComparison.OrdinalIgnoreCase))
        {
            quoteSymbol = sellSymbol;
            baseSymbol = pair.Substring(0, pair.Length - sellSymbol.Length);
            var buyIsBase = string.Equals(buySymbol, baseSymbol, StringComparison.OrdinalIgnoreCase);
            side = buyIsBase ? "BUY" : "SELL";
            baseAmount = buyIsBase ? buyAmount : sellAmount;
            quoteAmount = buyIsBase ? sellAmount : buyAmount;
        }
        else
        {
            side = "BUY";
            baseSymbol = buySymbol;
            quoteSymbol = sellSymbol;
            baseAmount = buyAmount;
            quoteAmount = sellAmount;
        }

        if (string.IsNullOrWhiteSpace(baseSymbol))
        {
            baseSymbol = buySymbol;
        }

        if (string.IsNullOrWhiteSpace(quoteSymbol))
        {
            quoteSymbol = sellSymbol;
        }

        var price = baseAmount != 0m ? quoteAmount / baseAmount : 0m;
        var feeSymbol = !string.IsNullOrWhiteSpace(quoteSymbol) ? quoteSymbol : baseSymbol;

        return new BinanceTrade
        {
            Date = ParseDateTimeOffset(GetValue(row, "dateupdated")),
            Pair = pair,
            Side = side,
            Price = price,
            Executed = $"{baseAmount.ToString(CultureEn)}{baseSymbol}",
            Amount = $"{quoteAmount.ToString(CultureEn)}{quoteSymbol}",
            Fee = $"0{feeSymbol}"
        };
    }

    private static BinanceStatementRow? ParseBinanceStatementRowData(Dictionary<string, string?> row, bool isExcel)
    {
        var operation = GetValue(row, "operation") ?? string.Empty;
        var coin = GetValue(row, "coin") ?? "UNKNOWN";
        var change = ParseDecimal(GetValue(row, "change")) ?? 0m;
        var remark = GetValue(row, "remark") ?? string.Empty;
        var hasUtcHeader = HasUtcHeader(row);
        var date = ParseBinanceStatementTime(GetValue(row, "utctime", "time", "date"), isExcel, hasUtcHeader);

        return new BinanceStatementRow(date, operation, coin, change, remark);
    }

    private static IEnumerable<List<BinanceStatementRow>> GroupStatementRows(IReadOnlyList<BinanceStatementRow> rows)
    {
        var grouped = new List<List<BinanceStatementRow>>();
        List<BinanceStatementRow>? current = null;
        DateTimeOffset? currentTime = null;

        foreach (var row in rows)
        {
            if (current == null || currentTime != row.Date)
            {
                if (current != null && current.Count > 0)
                {
                    grouped.Add(current);
                }

                current = new List<BinanceStatementRow>();
                currentTime = row.Date;
            }

            current.Add(row);
        }

        if (current != null && current.Count > 0)
        {
            grouped.Add(current);
        }

        return grouped;
    }

    private static BinanceTrade? BuildTradeFromStatementGroup(IReadOnlyList<BinanceStatementRow> group)
    {
        if (group.Count == 0)
        {
            return null;
        }

        if (group.Any(row => ContainsOperation(row.Operation, "transaction buy")))
        {
            return BuildBuyTrade(group);
        }

        if (group.Any(row => ContainsOperation(row.Operation, "transaction sold") || ContainsOperation(row.Operation, "transaction revenue")))
        {
            return BuildSellTrade(group);
        }

        if (group.Any(row => ContainsOperation(row.Operation, "binance convert")))
        {
            return BuildConvertTrade(group);
        }

        if (group.Any(row => IsExactOperation(row.Operation, "buy")) &&
            group.Any(row => IsExactOperation(row.Operation, "sell")))
        {
            var buyRow = group.FirstOrDefault(row => IsExactOperation(row.Operation, "buy"));
            var sellRow = group.FirstOrDefault(row => IsExactOperation(row.Operation, "sell"));

            if (buyRow != null && sellRow != null)
            {
                if (buyRow.Change >= 0m && sellRow.Change <= 0m)
                {
                    return BuildBuyTrade(group);
                }

                if (buyRow.Change <= 0m && sellRow.Change >= 0m)
                {
                    return BuildSellTrade(group);
                }
            }

            var buySum = group.Where(row => IsExactOperation(row.Operation, "buy")).Sum(row => row.Change);
            var sellSum = group.Where(row => IsExactOperation(row.Operation, "sell")).Sum(row => row.Change);

            if (buySum >= 0m && sellSum <= 0m)
            {
                return BuildBuyTrade(group);
            }

            if (buySum <= 0m && sellSum >= 0m)
            {
                return BuildSellTrade(group);
            }

            return BuildBuyTrade(group);
        }

        return null;
    }

    private static BinanceTrade? BuildBuyTrade(IReadOnlyList<BinanceStatementRow> group)
    {
        var feeRow = FindOperation(group, "transaction fee") ?? FindOperation(group, "fee");
        var buyRow = FindOperation(group, "transaction buy") ??
                     group.FirstOrDefault(r => IsExactOperation(r.Operation, "buy")) ??
                     group.Where(r => r.Change > 0).OrderByDescending(r => Math.Abs(r.Change)).FirstOrDefault();
        var spendRow = FindOperation(group, "transaction spend") ??
                       group.FirstOrDefault(r => IsExactOperation(r.Operation, "sell")) ??
                       FindOperation(group, "withdraw") ??
                       group.Where(r => r.Change < 0 && r != feeRow).OrderByDescending(r => Math.Abs(r.Change)).FirstOrDefault();

        if (buyRow == null || spendRow == null)
        {
            return null;
        }

        var baseAmount = Math.Abs(buyRow.Change);
        var quoteAmount = Math.Abs(spendRow.Change);
        var feeAmount = Math.Abs(feeRow?.Change ?? 0m);
        var feeSymbol = feeRow?.Coin ?? spendRow.Coin;
        var price = baseAmount != 0m ? quoteAmount / baseAmount : 0m;

        return new BinanceTrade
        {
            Date = buyRow.Date,
            Pair = $"{buyRow.Coin}{spendRow.Coin}",
            Side = "BUY",
            Price = price,
            Executed = $"{baseAmount.ToString(CultureEn)}{buyRow.Coin}",
            Amount = $"{quoteAmount.ToString(CultureEn)}{spendRow.Coin}",
            Fee = $"{feeAmount.ToString(CultureEn)}{feeSymbol}"
        };
    }

    private static BinanceTrade? BuildSellTrade(IReadOnlyList<BinanceStatementRow> group)
    {
        var feeRow = FindOperation(group, "transaction fee") ?? FindOperation(group, "fee");
        var soldRow = FindOperation(group, "transaction sold") ??
                      group.FirstOrDefault(r => IsExactOperation(r.Operation, "sell")) ??
                      group.Where(r => r.Change < 0 && r != feeRow).OrderByDescending(r => Math.Abs(r.Change)).FirstOrDefault();
        var revenueRow = FindOperation(group, "transaction revenue") ??
                         group.FirstOrDefault(r => IsExactOperation(r.Operation, "buy")) ??
                         group.Where(r => r.Change > 0).OrderByDescending(r => Math.Abs(r.Change)).FirstOrDefault();

        if (soldRow == null || revenueRow == null)
        {
            return null;
        }

        var baseAmount = Math.Abs(soldRow.Change);
        var quoteAmount = Math.Abs(revenueRow.Change);
        var feeAmount = Math.Abs(feeRow?.Change ?? 0m);
        var feeSymbol = feeRow?.Coin ?? soldRow.Coin;
        var price = baseAmount != 0m ? quoteAmount / baseAmount : 0m;

        return new BinanceTrade
        {
            Date = soldRow.Date,
            Pair = $"{soldRow.Coin}{revenueRow.Coin}",
            Side = "SELL",
            Price = price,
            Executed = $"{baseAmount.ToString(CultureEn)}{soldRow.Coin}",
            Amount = $"{quoteAmount.ToString(CultureEn)}{revenueRow.Coin}",
            Fee = $"{feeAmount.ToString(CultureEn)}{feeSymbol}"
        };
    }

    private static BinanceTrade? BuildConvertTrade(IReadOnlyList<BinanceStatementRow> group)
    {
        var positive = group.Where(r => r.Change > 0).OrderByDescending(r => Math.Abs(r.Change)).FirstOrDefault();
        var negative = group.Where(r => r.Change < 0).OrderByDescending(r => Math.Abs(r.Change)).FirstOrDefault();
        if (positive == null || negative == null)
        {
            return null;
        }

        var baseAmount = Math.Abs(positive.Change);
        var quoteAmount = Math.Abs(negative.Change);
        var price = baseAmount != 0m ? quoteAmount / baseAmount : 0m;

        return new BinanceTrade
        {
            Date = positive.Date,
            Pair = $"{positive.Coin}{negative.Coin}",
            Side = "BUY",
            Price = price,
            Executed = $"{baseAmount.ToString(CultureEn)}{positive.Coin}",
            Amount = $"{quoteAmount.ToString(CultureEn)}{negative.Coin}",
            Fee = $"0{negative.Coin}"
        };
    }

    private static BinanceStatementRow? FindOperation(IEnumerable<BinanceStatementRow> group, string needle)
        => group.FirstOrDefault(row => ContainsOperation(row.Operation, needle));

    private static bool ContainsOperation(string? operation, string needle)
        => !string.IsNullOrWhiteSpace(operation) && operation.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static bool IsExactOperation(string? operation, string expected)
        => !string.IsNullOrWhiteSpace(operation) &&
           string.Equals(operation.Trim(), expected, StringComparison.OrdinalIgnoreCase);

    private static (BinanceDeposit deposit, BinanceWithdrawal withdrawal)? ParseBinanceStatementRow(Dictionary<string, string?> row, bool isDeposit)
    {
        var date = ParseDateTimeOffset(GetValue(row, "utctime", "time", "date"));
        var coin = GetValue(row, "coin");
        var change = ParseDecimal(GetValue(row, "change"));
        if (string.IsNullOrWhiteSpace(coin) || change == null)
        {
            return null;
        }

        var amount = Math.Abs(change.Value);
        if (isDeposit)
        {
            return (new BinanceDeposit
            {
                Date = date,
                Coin = coin,
                Network = string.Empty,
                Amount = amount,
                TransactionFee = 0m,
                Address = string.Empty,
                TXID = string.Empty,
                Comment = GetValue(row, "remark") ?? string.Empty
            }, null!);
        }

        return (null!, new BinanceWithdrawal
        {
            Date = date,
            Coin = coin,
            Network = string.Empty,
            Amount = amount,
            TransactionFee = 0m,
            Address = string.Empty,
            TXID = string.Empty,
            Comment = GetValue(row, "remark") ?? string.Empty
        });
    }

    private static BitcoinDeTransaction? ParseBitcoinDeAccountRow(Dictionary<string, string?> row)
    {
        var date = ParseDateTimeDe(GetValue(row, "datum"));
        var typ = GetValue(row, "typ") ?? string.Empty;
        var symbol = GetValue(row, "waehrung", "wahrung") ?? string.Empty;

        if (date == null || string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        return new BitcoinDeTransaction
        {
            Datum = date.Value,
            Typ = typ,
            Waehrung = symbol,
            Referenz = GetValue(row, "referenz") ?? string.Empty,
            Adresse = GetValueBySuffix(row, "adresse") ?? string.Empty,
            Kurs = ParseDecimalDe(GetValue(row, "kurs")),
            EinheitKurs = GetValue(row, "einheitkurs") ?? string.Empty,
            CryptoVorGebuehr = ParseDecimalDe(GetValueBySuffix(row, "vorgebuhr")),
            MengeVorGebuehr = ParseDecimalDe(GetValue(row, "mengevorgebuhr")),
            EinheitMengeVorGebuehr = GetValue(row, "einheitmengevorgebuhr") ?? string.Empty,
            CryptoNachGebuehr = ParseDecimalDe(GetValueBySuffix(row, "nachbitcoindegebuhr", "nachgebuhr")),
            MengeNachGebuehr = ParseDecimalDe(GetValue(row, "mengenachbitcoindegebuhr", "mengenachgebuhr")),
            EinheitMengeNachGebuehr = GetValue(row, "einheitmengenachbitcoindegebuhr", "einheitmengenachgebuhr") ?? string.Empty,
            ZuAbgang = ParseDecimalDe(GetValue(row, "zuabgang")) ?? 0m,
            Kontostand = ParseDecimalDe(GetValue(row, "kontostand")) ?? 0m,
            Kommentar = GetValue(row, "kommentar") ?? string.Empty
        };
    }

    private static BitcoinDeTransaction? ParseBitcoinDeBuySellRow(Dictionary<string, string?> row, string typ)
    {
        var date = ParseDateTimeDe(GetValue(row, "datum"));
        var symbol = GetValue(row, "symbol") ?? string.Empty;
        if (date == null || string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        var amountCrypto = ParseDecimalDe(GetValue(row, "menge")) ?? 0m;
        var amountFiat = ParseDecimalDe(GetValue(row, "betrag")) ?? 0m;
        var kurs = ParseDecimalDe(GetValue(row, "kurs"));

        return new BitcoinDeTransaction
        {
            Datum = date.Value,
            Typ = typ,
            Waehrung = symbol,
            Referenz = string.Empty,
            Adresse = string.Empty,
            Kurs = kurs,
            EinheitKurs = $"{symbol} / EUR",
            CryptoVorGebuehr = amountCrypto,
            MengeVorGebuehr = amountFiat,
            EinheitMengeVorGebuehr = "EUR",
            CryptoNachGebuehr = amountCrypto,
            MengeNachGebuehr = amountFiat,
            EinheitMengeNachGebuehr = "EUR",
            ZuAbgang = 0m,
            Kontostand = 0m,
            Kommentar = string.Empty
        };
    }

    private static BitcoinDeTransaction? ParseBitcoinDeDepositRow(Dictionary<string, string?> row)
    {
        var date = ParseDateTimeDe(GetValue(row, "datum"));
        var symbol = GetValue(row, "symbol") ?? string.Empty;
        var amount = ParseDecimalDe(GetValue(row, "menge")) ?? 0m;
        if (date == null || string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        return new BitcoinDeTransaction
        {
            Datum = date.Value,
            Typ = "Einzahlung",
            Waehrung = symbol,
            Referenz = string.Empty,
            Adresse = string.Empty,
            ZuAbgang = amount,
            Kontostand = 0m,
            Kommentar = string.Empty
        };
    }

    private static BitcoinDeTransaction? ParseBitcoinDeWithdrawalRow(Dictionary<string, string?> row)
    {
        var date = ParseDateTimeDe(GetValue(row, "datum"));
        var symbol = GetValue(row, "symbol") ?? string.Empty;
        var amount = ParseDecimalDe(GetValue(row, "menge")) ?? 0m;
        if (date == null || string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        return new BitcoinDeTransaction
        {
            Datum = date.Value,
            Typ = "Auszahlung",
            Waehrung = symbol,
            Referenz = string.Empty,
            Adresse = GetValue(row, "adresse") ?? string.Empty,
            ZuAbgang = -amount,
            Kontostand = 0m,
            Kommentar = GetValue(row, "kommentar") ?? string.Empty
        };
    }

    private static BitpandaTransaction? ParseBitpandaRow(Dictionary<string, string?> row)
    {
        var id = GetValue(row, "transactionid") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return new BitpandaTransaction
        {
            TransactionId = id,
            Timestamp = ParseDateTimeOffset(GetValue(row, "timestamp")),
            TransactionType = GetValue(row, "transactiontype") ?? string.Empty,
            InOut = GetValue(row, "inout") ?? string.Empty,
            AmountFiat = ParseDecimal(GetValue(row, "amountfiat")),
            Fiat = GetValue(row, "fiat") ?? string.Empty,
            AmountAsset = ParseDecimal(GetValue(row, "amountasset")),
            Asset = GetValue(row, "asset") ?? string.Empty,
            AssetMarketPrice = ParseDecimal(GetValue(row, "assetmarketprice")),
            AssetMarketPriceCurrency = GetValue(row, "assetmarketpricecurrency") ?? string.Empty,
            AssetClass = GetValue(row, "assetclass") ?? string.Empty,
            ProductID = ParseInt(GetValue(row, "productid")),
            Fee = ParseDecimal(GetValue(row, "fee")),
            FeeAsset = GetValue(row, "feeasset") ?? string.Empty,
            Spread = ParseDecimal(GetValue(row, "spread")),
            SpreadCurrency = GetValue(row, "spreadcurrency") ?? string.Empty,
            TaxFiat = ParseDecimal(GetValue(row, "taxfiat")),
            Address = GetValue(row, "address") ?? string.Empty,
            Comment = GetValue(row, "comment") ?? string.Empty
        };
    }

    private static MetamaskTransaction? ParseMetamaskTransactionRow(Dictionary<string, string?> row)
    {
        var coin = GetValue(row, "coin");
        if (string.IsNullOrWhiteSpace(coin))
        {
            return null;
        }

        return new MetamaskTransaction
        {
            Datum = ParseDateTimeOffset(GetValue(row, "datum", "datumutc")),
            Typ = GetValue(row, "typ") ?? string.Empty,
            Coin = coin,
            Network = GetValue(row, "network") ?? string.Empty,
            Amount = ParseDecimal(GetValue(row, "amount")) ?? 0m,
            TransactionFee = ParseDecimal(GetValue(row, "transactionfee")) ?? 0m,
            Kommentar = GetValue(row, "kommentar") ?? string.Empty
        };
    }

    private static LedgerTransaction? ParseLedgerTransactionRow(Dictionary<string, string?> row)
    {
        var coin = GetValue(row, "coin");
        if (string.IsNullOrWhiteSpace(coin))
        {
            return null;
        }

        return new LedgerTransaction
        {
            Datum = ParseDateTimeOffset(GetValue(row, "datumutc", "datum")),
            Typ = GetValue(row, "typ") ?? string.Empty,
            Coin = coin,
            Network = GetValue(row, "network") ?? string.Empty,
            Address = GetValue(row, "address") ?? string.Empty,
            Amount = ParseDecimal(GetValue(row, "amount")) ?? 0m,
            TransactionFee = ParseDecimal(GetValue(row, "transactionfee")) ?? 0m,
            Kommentar = GetValue(row, "kommentar") ?? string.Empty
        };
    }

    private static MetamaskTrade? ParseMetamaskTradeRow(Dictionary<string, string?> row)
    {
        var pair = GetValue(row, "pair");
        if (string.IsNullOrWhiteSpace(pair))
        {
            return null;
        }

        return new MetamaskTrade
        {
            Date = ParseDateTimeOffset(GetValue(row, "dateutc")),
            Pair = pair,
            Side = GetValue(row, "side") ?? string.Empty,
            Price = GetValue(row, "price") ?? string.Empty,
            Executed = GetValue(row, "executed") ?? string.Empty,
            Amount = GetValue(row, "amount") ?? string.Empty,
            Fee = GetValue(row, "fee") ?? string.Empty,
            Tradingplatform = GetValue(row, "tradingplatform") ?? string.Empty
        };
    }

    private static OkxDeposit? ParseOkxDepositRow(Dictionary<string, string?> row)
    {
        var coin = GetValue(row, "coin");
        if (string.IsNullOrWhiteSpace(coin))
        {
            return null;
        }

        return new OkxDeposit
        {
            Date = ParseDateTimeOffset(GetValue(row, "dateutc")),
            Coin = coin,
            Network = GetValue(row, "network") ?? string.Empty,
            Amount = ParseDecimal(GetValue(row, "amount")) ?? 0m,
            Address = GetValue(row, "address") ?? string.Empty,
            Kommentar = GetValue(row, "kommentar", "comment") ?? string.Empty
        };
    }

    private static OkxTrade? ParseOkxTradeRow(Dictionary<string, string?> row)
    {
        var pair = GetValue(row, "pair");
        if (string.IsNullOrWhiteSpace(pair))
        {
            return null;
        }

        return new OkxTrade
        {
            Date = ParseDateTimeOffset(GetValue(row, "dateutc")),
            Pair = pair,
            Side = GetValue(row, "side") ?? string.Empty,
            Price = GetValue(row, "price") ?? string.Empty,
            Executed = ParseDecimal(GetValue(row, "executed")) ?? 0m
        };
    }

    private static string NormalizeType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return string.Empty;
        }

        var normalized = type.Trim().ToLowerInvariant();
        return normalized switch
        {
            "deposit" => "Einzahlung",
            "withdrawal" => "Auszahlung",
            "withdraw" => "Auszahlung",
            "eingang" => "Einzahlung",
            "ausgang" => "Auszahlung",
            _ => type
        };
    }

    private static string NormalizeTransferType(string? inOut)
    {
        if (string.IsNullOrWhiteSpace(inOut))
        {
            return "Transfer";
        }

        return inOut.Trim().ToLowerInvariant() switch
        {
            "incoming" => "Einzahlung",
            "outgoing" => "Auszahlung",
            _ => "Transfer"
        };
    }

    private static string? GetValue(Dictionary<string, string?> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            var normalized = NormalizeHeader(key);
            if (row.TryGetValue(normalized, out var value))
            {
                return value?.Trim();
            }
        }

        return null;
    }

    private static string? GetValueBySuffix(Dictionary<string, string?> row, params string[] suffixes)
    {
        foreach (var suffix in suffixes)
        {
            var normalizedSuffix = NormalizeHeader(suffix);
            foreach (var kvp in row)
            {
                if (kvp.Key.EndsWith(normalizedSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value?.Trim();
                }
            }
        }

        return null;
    }

    private static decimal? ParseDecimal(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();
        if (trimmed == "-")
        {
            return null;
        }

        var numeric = ExtractNumeric(trimmed);
        if (string.IsNullOrWhiteSpace(numeric))
        {
            return null;
        }

        var (primary, secondary) = ResolveDecimalCultures(numeric);
        if (decimal.TryParse(numeric, NumberStyles.Float | NumberStyles.AllowThousands, primary, out var result))
        {
            return result;
        }

        if (decimal.TryParse(numeric, NumberStyles.Float | NumberStyles.AllowThousands, secondary, out result))
        {
            return result;
        }

        if (decimal.TryParse(numeric, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result))
        {
            return result;
        }

        return null;
    }

    private static string FormatDecimal(decimal value)
        => value.ToString(CultureEn);

    private static (CultureInfo primary, CultureInfo secondary) ResolveDecimalCultures(string numeric)
    {
        var hasComma = numeric.Contains(',');
        var hasDot = numeric.Contains('.');
        if (hasComma && hasDot)
        {
            var lastComma = numeric.LastIndexOf(',');
            var lastDot = numeric.LastIndexOf('.');
            var primary = lastComma > lastDot ? CultureDe : CultureEn;
            return primary == CultureDe ? (CultureDe, CultureEn) : (CultureEn, CultureDe);
        }

        if (hasComma)
        {
            var primary = numeric.Count(ch => ch == ',') > 1 ? CultureEn : CultureDe;
            return primary == CultureDe ? (CultureDe, CultureEn) : (CultureEn, CultureDe);
        }

        return (CultureEn, CultureDe);
    }

    private static bool IsBinanceStatementDeposit(string? operation, decimal change)
    {
        if (change > 0m)
        {
            return true;
        }

        if (change < 0m)
        {
            return false;
        }

        var trimmed = operation?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return true;
        }

        if (BinanceStatementDepositOperations.Contains(trimmed))
        {
            return true;
        }

        if (BinanceStatementWithdrawalOperations.Contains(trimmed))
        {
            return false;
        }

        var lower = trimmed.ToLowerInvariant();
        if (lower.Contains("staking") || lower.Contains("reward") || lower.Contains("interest") || lower.Contains("distribution") ||
            lower.Contains("airdrop") || lower.Contains("earn"))
        {
            return true;
        }

        if (lower.Contains("fee") || lower.Contains("spend") || lower.Contains("withdraw"))
        {
            return false;
        }

        return true;
    }

    private static string BuildOperationComment(string? operation, string? remark)
    {
        var op = operation?.Trim();
        var rm = remark?.Trim();
        if (string.IsNullOrWhiteSpace(op))
        {
            return rm ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(rm) || string.Equals(op, rm, StringComparison.OrdinalIgnoreCase))
        {
            return op;
        }

        return $"{op} - {rm}";
    }

    private static int? ParseInt(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Trim() == "-")
        {
            return null;
        }

        if (int.TryParse(input.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return null;
    }

    private static decimal? ParseDecimalDe(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();
        if (trimmed == "-")
        {
            return null;
        }

        var numeric = ExtractNumeric(trimmed);
        var hasComma = trimmed.Contains(',');
        var hasDot = trimmed.Contains('.');
        var primary = hasComma && !hasDot ? CultureDe : hasDot && !hasComma ? CultureEn : CultureDe;
        var secondary = primary == CultureDe ? CultureEn : CultureDe;

        if (decimal.TryParse(numeric, NumberStyles.Float | NumberStyles.AllowThousands, primary, out var result))
        {
            return result;
        }

        if (decimal.TryParse(numeric, NumberStyles.Float | NumberStyles.AllowThousands, secondary, out result))
        {
            return result;
        }

        if (decimal.TryParse(numeric, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result))
        {
            return result;
        }

        return null;
    }

    private static (decimal amount, string symbol) ParseAmountWithSymbol(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return (0m, string.Empty);
        }

        var trimmed = input.Trim();
        var splitIndex = trimmed.IndexOf(' ');
        string numberStr;
        string symbolStr;
        if (splitIndex > 0)
        {
            numberStr = trimmed.Substring(0, splitIndex);
            symbolStr = trimmed.Substring(splitIndex + 1);
        }
        else
        {
            var i = 0;
            while (i < trimmed.Length && (char.IsDigit(trimmed[i]) || trimmed[i] == '.' || trimmed[i] == ',' || trimmed[i] == '-' || trimmed[i] == 'E' || trimmed[i] == 'e' || trimmed[i] == '+'))
            {
                i++;
            }

            numberStr = i == 0 ? "0" : trimmed.Substring(0, i);
            symbolStr = i >= trimmed.Length ? string.Empty : trimmed.Substring(i);
        }

        var amount = ParseDecimal(numberStr) ?? 0m;
        return (amount, symbolStr.Trim());
    }

    private static string InferCoinFromAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return "BTC";
        }

        var trimmed = address.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return "ETH";
        }

        if (trimmed.StartsWith("bc1", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("1") || trimmed.StartsWith("3"))
        {
            return "BTC";
        }

        if (trimmed.StartsWith("ltc1", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("L") || trimmed.StartsWith("M"))
        {
            return "LTC";
        }

        if (trimmed.StartsWith("r"))
        {
            return "XRP";
        }

        return "BTC";
    }

    private static string ExtractNumeric(string value)
    {
        var sb = new StringBuilder();
        foreach (var ch in value)
        {
            if (char.IsDigit(ch) || ch == '.' || ch == ',' || ch == '-' || ch == '+' || ch == 'e' || ch == 'E')
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static string EnsureTxId(string? txid, params string?[] parts)
    {
        if (!string.IsNullOrWhiteSpace(txid))
        {
            return txid.Trim();
        }

        var fallback = string.Join("|", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        return string.IsNullOrWhiteSpace(fallback) ? Guid.NewGuid().ToString("N") : fallback;
    }

    private static (string baseSymbol, string quoteSymbol) SplitBinancePair(string? pair)
    {
        if (string.IsNullOrWhiteSpace(pair))
        {
            return (string.Empty, string.Empty);
        }

        foreach (var quote in BinanceQuoteAssets)
        {
            if (pair.EndsWith(quote, StringComparison.OrdinalIgnoreCase) && pair.Length > quote.Length)
            {
                return (pair.Substring(0, pair.Length - quote.Length), quote);
            }
        }

        if (pair.Length > 3)
        {
            return (pair.Substring(0, pair.Length - 3), pair.Substring(pair.Length - 3));
        }

        return (pair, string.Empty);
    }

    private static DateTimeOffset ParseDateTimeOffset(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return DateTimeOffset.MinValue;
        }

        if (DateTimeOffset.TryParse(input, CultureDe, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
        {
            return dto;
        }

        if (DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dto))
        {
            return dto;
        }

        if (DateTimeOffset.TryParse(input, CultureEn, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dto))
        {
            return dto;
        }

        return DateTimeOffset.MinValue;
    }

    private static DateTimeOffset ParseBinanceExchangeTime(string? input, bool isExcel, bool hasUtcHeader)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return DateTimeOffset.MinValue;
        }

        var normalized = Regex.Replace(input.Trim(), "\\s+", " ");
        var formats = new[]
        {
            "yy-MM-dd HH:mm:ss",
            "yy-MM-dd HH:mm",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "dd.MM.yyyy HH:mm:ss",
            "dd.MM.yyyy HH:mm",
            "dd.MM.yy HH:mm:ss",
            "dd.MM.yy HH:mm"
        };

        if (DateTime.TryParseExact(normalized, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ||
            DateTime.TryParseExact(normalized, formats, CultureDe, DateTimeStyles.None, out dt) ||
            DateTime.TryParseExact(normalized, formats, CultureEn, DateTimeStyles.None, out dt))
        {
            var offset = isExcel && !hasUtcHeader && !HasExplicitTimeZone(input) ? TimeSpan.FromHours(1) : TimeSpan.Zero;
            return new DateTimeOffset(dt, offset).ToOffset(TimeSpan.Zero);
        }

        var parsed = ParseDateTimeOffset(input);
        if (parsed == DateTimeOffset.MinValue || !isExcel)
        {
            return parsed;
        }

        if (hasUtcHeader || HasExplicitTimeZone(input))
        {
            return parsed;
        }

        return parsed.AddHours(-1);
    }

    private static bool HasExplicitTimeZone(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (trimmed.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmed.Contains("UTC", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Regex.IsMatch(trimmed, @"[+-]\d{2}:?\d{2}$");
    }

    private static DateTimeOffset ParseBinanceStatementTime(string? input, bool isExcel, bool hasUtcHeader)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return DateTimeOffset.MinValue;
        }

        var normalized = Regex.Replace(input.Trim(), "\\s+", " ");
        var formats = new[]
        {
            "yy-MM-dd HH:mm:ss",
            "yy-MM-dd HH:mm",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "dd.MM.yyyy HH:mm:ss",
            "dd.MM.yyyy HH:mm",
            "dd.MM.yy HH:mm:ss",
            "dd.MM.yy HH:mm"
        };

        if (DateTime.TryParseExact(normalized, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ||
            DateTime.TryParseExact(normalized, formats, CultureDe, DateTimeStyles.None, out dt) ||
            DateTime.TryParseExact(normalized, formats, CultureEn, DateTimeStyles.None, out dt) ||
            DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt) ||
            DateTime.TryParse(normalized, CultureDe, DateTimeStyles.None, out dt) ||
            DateTime.TryParse(normalized, CultureEn, DateTimeStyles.None, out dt))
        {
            var offset = isExcel && !hasUtcHeader ? TimeSpan.FromHours(1) : TimeSpan.Zero;
            return new DateTimeOffset(dt, offset).ToOffset(TimeSpan.Zero);
        }

        return DateTimeOffset.MinValue;
    }

    private static bool HasUtcHeader(IReadOnlyDictionary<string, string?> row)
        => row.Keys.Any(key => key.Contains("utc", StringComparison.OrdinalIgnoreCase));

    private static DateTime? ParseDateTimeDe(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        if (DateTime.TryParse(input, CultureDe, DateTimeStyles.None, out var dt))
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        if (DateTime.TryParse(input, CultureEn, DateTimeStyles.None, out dt))
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        return null;
    }

    private static DateTime? ParseDateTime(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
        {
            return dt;
        }

        if (DateTime.TryParse(input, CultureDe, DateTimeStyles.AssumeLocal, out dt))
        {
            return dt;
        }

        if (DateTime.TryParse(input, CultureEn, DateTimeStyles.AssumeLocal, out dt))
        {
            return dt;
        }

        return null;
    }
}
