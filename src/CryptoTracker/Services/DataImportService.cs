using CryptoTracker.Entities;
using CryptoTracker.Import;
using CryptoTracker.Shared;
using CryptoTracker.Import.Objects;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Services
{
    public class DataImportService
    {
        private readonly CryptoTrackerDbContext _dbContext;

        public DataImportService(CryptoTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task Import(ImportDocumentType type, string walletName, Func<Stream> openStreamFunc)
        {
            await StoreRawEntries(type, openStreamFunc);

            var importer = GetImporter(type);
            var wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Name == walletName);
            if (wallet == null)
            {
                wallet = new Wallet { Name = walletName };
                _dbContext.Wallets.Add(wallet);
                await _dbContext.SaveChangesAsync();
            }
            await importer.Import(new ImportArgs() { Wallet = wallet }, openStreamFunc);
        }

        private async Task StoreRawEntries(ImportDocumentType type, Func<Stream> openStreamFunc)
        {
            switch (type)
            {
                case ImportDocumentType.BinanceDepositHistory:
                    await SaveEntries<BinanceDeposit>(type, openStreamFunc);
                    break;
                case ImportDocumentType.BinanceWithdrawalHistory:
                    await SaveEntries<BinanceWithdrawal>(type, openStreamFunc);
                    break;
                case ImportDocumentType.BinanceTradingHistory:
                    await SaveEntries<BinanceTrade>(type, openStreamFunc);
                    break;
                case ImportDocumentType.BitcoinDeTransactions:
                    await SaveEntries<BitcoinDeTransaction>(type, openStreamFunc);
                    break;
                case ImportDocumentType.BitpandaTransaction:
                    await SaveEntries<BitpandaTransaction>(type, openStreamFunc);
                    break;
                case ImportDocumentType.MetamaskTradingHistory:
                    await SaveEntries<MetamaskTrade>(type, openStreamFunc);
                    break;
                case ImportDocumentType.MetamaskTransactions:
                    await SaveEntries<MetamaskTransaction>(type, openStreamFunc);
                    break;
                case ImportDocumentType.OkxDepositHistory:
                    await SaveEntries<OkxDeposit>(type, openStreamFunc);
                    break;
                case ImportDocumentType.OkxTradingHistory:
                    await SaveEntries<OkxTrade>(type, openStreamFunc);
                    break;
                default:
                    break;
            }
        }

        private async Task SaveEntries<T>(ImportDocumentType type, Func<Stream> openStreamFunc) where T : class
        {
            using var stream = openStreamFunc();
            using var reader = new StreamReader(stream);
            var (config, setup) = GetCsvSetup(type);
            using var csv = new CsvReader(reader, config);
            setup?.Invoke(csv);
            var records = csv.GetRecords<T>().ToList();

            _dbContext.Set<T>().AddRange(records);
            await _dbContext.SaveChangesAsync();
        }

        private (CsvConfiguration config, Action<CsvReader>? setup) GetCsvSetup(ImportDocumentType type)
        {
            switch (type)
            {
                case ImportDocumentType.BinanceDepositHistory:
                case ImportDocumentType.BinanceWithdrawalHistory:
                case ImportDocumentType.OkxDepositHistory:
                case ImportDocumentType.OkxTradingHistory:
                case ImportDocumentType.MetamaskTradingHistory:
                case ImportDocumentType.MetamaskTransactions:
                    return (new CsvConfiguration(new CultureInfo("de-AT")) { Delimiter = ";" }, reader => reader.Context.TypeConverterCache.AddConverter<DateTimeOffset>(new UtcDateTimeConverter()));
                case ImportDocumentType.BinanceTradingHistory:
                    return (new CsvConfiguration(new CultureInfo("en-US")), reader => reader.Context.TypeConverterCache.AddConverter<DateTimeOffset>(new UtcDateTimeConverter()));
                case ImportDocumentType.BitcoinDeTransactions:
                    return (new CsvConfiguration(new CultureInfo("en-US")) { Delimiter = ";" }, null);
                case ImportDocumentType.BitpandaTransaction:
                    return (new CsvConfiguration(new CultureInfo("en-US")), reader =>
                    {
                        reader.Context.TypeConverterCache.AddConverter<decimal>(new BitpandaDecimalConverter());
                        reader.Context.TypeConverterCache.AddConverter<int>(new BitpandaIntConverter());
                    });
                default:
                    return (new CsvConfiguration(CultureInfo.InvariantCulture), null);
            }
        }

        private class BitpandaDecimalConverter : CsvHelper.TypeConversion.DecimalConverter
        {
            public override object? ConvertFromString(string? text, CsvHelper.IReaderRow row, CsvHelper.Configuration.MemberMapData memberMapData)
            {
                if (text == "-")
                    text = string.Empty;
                if (decimal.TryParse(text, new CultureInfo("en-US"), out var result))
                    return result;
                return null;
            }
        }

        private class BitpandaIntConverter : CsvHelper.TypeConversion.Int32Converter
        {
            public override object? ConvertFromString(string? text, CsvHelper.IReaderRow row, CsvHelper.Configuration.MemberMapData memberMapData)
            {
                if (text == "-")
                    text = string.Empty;
                if (int.TryParse(text, new CultureInfo("en-US"), out var result))
                    return result;
                return null;
            }
        }

        public async Task ProcessTransactionPairs()
        {
            var transactions = await _dbContext.CryptoTransactions.ToListAsync();

            foreach (var transaction in transactions.Where(t => t.TransactionType == TransactionType.Send))
            {
                var oppositeTransaction = transactions
                    .Where(t => t.TransactionType == TransactionType.Receive &&
                                t.QuantityAfterFee == transaction.QuantityAfterFee &&
                                t.DateTime >= transaction.DateTime.AddMinutes(-1) /* die Sekunden müssen nicht immer stimmen */ &&
                                t.DateTime <= transaction.DateTime.AddHours(5))
                    .FirstOrDefault();

                if (oppositeTransaction != null)
                {
                    transaction.OppositeTransaction = oppositeTransaction;
                    transaction.OppositeWallet = oppositeTransaction.Wallet;
                    transaction.OppositeWalletId = oppositeTransaction.WalletId;
                    oppositeTransaction.OppositeTransaction = transaction;
                    oppositeTransaction.OppositeWallet = transaction.Wallet;
                    oppositeTransaction.OppositeWalletId = transaction.WalletId;
                }
            }

            await _dbContext.SaveChangesAsync();
        }

        private IImporter GetImporter(ImportDocumentType type)
            => type switch
            {
                ImportDocumentType.BinanceDepositHistory => new BinanceDepositImporter(_dbContext),
                ImportDocumentType.BinanceTradingHistory => new BinanceTradeImporter(_dbContext),
                ImportDocumentType.BinanceWithdrawalHistory => new BinanceWithdrawalImporter(_dbContext),
                ImportDocumentType.BitcoinDeTransactions => new BitcoinDeTransactionImporter(_dbContext),
                ImportDocumentType.BitpandaTransaction => new BitpandaTransactionImporter(_dbContext),
                ImportDocumentType.MetamaskTradingHistory => new MetamaskTradeImporter(_dbContext),
                ImportDocumentType.MetamaskTransactions => new MetamaskTransactionImporter(_dbContext),
                ImportDocumentType.OkxDepositHistory => new OkxDepositImporter(_dbContext),
                ImportDocumentType.OkxTradingHistory => new OkxTradeImporter(_dbContext),
                _ => throw new NotSupportedException()
            };
    }
}
