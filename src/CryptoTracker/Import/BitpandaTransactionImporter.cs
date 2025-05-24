using CryptoTracker.Entities;
using CryptoTracker.Import.Objects;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Globalization;

namespace CryptoTracker.Import
{
    public class BitpandaTransactionImporter : ImporterBase<BitpandaTransaction>
    {
        public BitpandaTransactionImporter(CryptoTrackerDbContext dbContext)
            : base(dbContext)
        {
        }

        protected override CsvConfiguration CreateCsvConfiguration()
            => new CsvConfiguration(new CultureInfo("en-US"));

        protected override void OnCsvReaderCreated(CsvReader reader)
        {
            base.OnCsvReaderCreated(reader);
            reader.Context.TypeConverterCache.AddConverter<decimal>(new BitpandaDecimalConverter());
            reader.Context.TypeConverterCache.AddConverter<int>(new BitpandaIntConverter());
        }

        private class BitpandaDecimalConverter : DecimalConverter
        {
            public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
            {
                if (text == "-")
                    text = "";

                if (decimal.TryParse(text, new CultureInfo("en-US"), out var parsedDecimal))
                    return parsedDecimal;
                else
                    return null;
            }
        }

        private class BitpandaIntConverter : Int32Converter
        {
            public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
            {
                if (text == "-")
                    text = "";

                if (int.TryParse(text, new CultureInfo("en-US"), out var parsedInteger))
                    return parsedInteger;
                else
                    return null;
            }
        }

        protected override async Task OnImport(ImportArgs args, IEnumerable<BitpandaTransaction> records)
        {
            var trades = new List<(CryptoTrade sellTrade, CryptoTrade buyTrade)>();
            foreach (var record in records)
            {
                if ((string.IsNullOrEmpty(record.AssetClass) || record.AssetClass == "Cryptocurrency") &&
                    (string.Equals(record.TransactionType, "buy", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(record.TransactionType, "sell", StringComparison.OrdinalIgnoreCase)))
                {
                    var price = record.AssetMarketPrice ?? (record.AmountAsset.HasValue && record.AmountFiat.HasValue && record.AmountAsset.Value != 0 ? record.AmountFiat.Value / record.AmountAsset.Value : 0);
                    var sellTrade = new CryptoTrade
                    {
                        WalletId = args.Wallet.Id,
                        DateTime = record.Timestamp,
                        Symbol = record.TransactionType.Equals("buy", StringComparison.OrdinalIgnoreCase) ? record.Fiat : record.Asset,
                        OpositeSymbol = record.TransactionType.Equals("buy", StringComparison.OrdinalIgnoreCase) ? record.Asset : record.Fiat,
                        TradeType = TradeType.Sell,
                        Price = record.TransactionType.Equals("buy", StringComparison.OrdinalIgnoreCase) ? 1 / price : price,
                        Quantity = record.TransactionType.Equals("buy", StringComparison.OrdinalIgnoreCase) ? record.AmountFiat!.Value : record.AmountAsset!.Value,
                        Fee = 0,
                        ForeignFee = 0,
                        ForeignFeeSymbol = string.Empty,
                        Referenz = record.TransactionId,
                    };
                    var buyTrade = new CryptoTrade
                    {
                        WalletId = args.Wallet.Id,
                        DateTime = record.Timestamp,
                        Symbol = record.TransactionType.Equals("sell", StringComparison.OrdinalIgnoreCase) ? record.Fiat : record.Asset,
                        OpositeSymbol = record.TransactionType.Equals("sell", StringComparison.OrdinalIgnoreCase) ? record.Asset : record.Fiat,
                        TradeType = TradeType.Buy,
                        Price = record.TransactionType.Equals("sell", StringComparison.OrdinalIgnoreCase) ? 1 / price : price,
                        Quantity = record.TransactionType.Equals("buy", StringComparison.OrdinalIgnoreCase) ? record.AmountAsset!.Value : record.AmountFiat!.Value,
                        Fee = 0,
                        ForeignFee = 0,
                        ForeignFeeSymbol = string.Empty,
                        Referenz = record.TransactionId,
                    };

                    DbContext.Add(sellTrade);
                    DbContext.Add(buyTrade);
                    trades.Add((sellTrade, buyTrade));
                }
                else if ((string.IsNullOrEmpty(record.AssetClass) || record.AssetClass == "Cryptocurrency") &&
                         (string.Equals(record.TransactionType, "deposit", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(record.TransactionType, "withdrawal", StringComparison.OrdinalIgnoreCase)))
                {
                    var transaction = new CryptoTransaction
                    {
                        TransactionType = record.TransactionType == "deposit" ? TransactionType.Receive : TransactionType.Send,
                        WalletId = args.Wallet.Id,
                        DateTime = record.Timestamp,
                        Symbol = record.Asset,
                        Quantity = record.AmountAsset.GetValueOrDefault() + record.Fee.GetValueOrDefault(),
                        Fee = record.Fee.GetValueOrDefault(),
                        TransactionId = record.TransactionId,
                        Address = record.Address,
                        Comment = record.Comment
                    };
                    DbContext.Add(transaction);
                }
            }

            await DbContext.SaveChangesAsync();

            foreach (var pair in trades)
            {
                pair.sellTrade.OppositeTrade = pair.buyTrade;
                pair.buyTrade.OppositeTrade = pair.sellTrade;
            }

            await DbContext.SaveChangesAsync();
        }
    }
}
