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
            public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
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
            public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
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
                if (record.AssetClass == "Cryptocurrency" && (record.TransactionType == "buy" || record.TransactionType == "sell"))
                {
                    var sellTrade = new CryptoTrade
                    {
                        Wallet = args.Wallet,
                        DateTime = record.Timestamp,
                        Symbol = record.TransactionType == "buy" ? record.Fiat : record.Asset,
                        OpositeSymbol = record.TransactionType == "buy" ? record.Asset : record.Fiat,
                        TradeType = TradeType.Sell,
                        Price = record.TransactionType == "buy" ? 1 / record.AssetMarketPrice!.Value : record.AssetMarketPrice!.Value,
                        Quantity = record.TransactionType == "buy" ? record.AmountFiat!.Value : record.AmountAsset!.Value,
                        Fee = 0,
                        ForeignFee = 0,
                        ForeignFeeSymbol = string.Empty,
                    };
                    var buyTrade = new CryptoTrade
                    {
                        Wallet = args.Wallet,
                        DateTime = record.Timestamp,
                        Symbol = record.TransactionType == "sell" ? record.Fiat : record.Asset,
                        OpositeSymbol = record.TransactionType == "sell" ? record.Asset : record.Fiat,
                        TradeType = TradeType.Buy,
                        Price = record.TransactionType == "sell" ? 1 / record.AssetMarketPrice!.Value : record.AssetMarketPrice!.Value,
                        Quantity = record.TransactionType == "buy" ? record.AmountAsset!.Value : record.AmountFiat!.Value,
                        Fee = 0,
                        ForeignFee = 0,
                        ForeignFeeSymbol = string.Empty,
                    };

                    DbContext.Add(sellTrade);
                    DbContext.Add(buyTrade);
                    trades.Add((sellTrade, buyTrade));
                }
                else if (record.AssetClass == "Cryptocurrency" && (record.TransactionType == "deposit" || record.TransactionType == "withdrawal"))
                {
                    var transaction = new CryptoTransaction
                    {
                        TransactionType = record.TransactionType == "deposit" ? TransactionType.Receive : TransactionType.Send,
                        Wallet = args.Wallet,
                        DateTime = record.Timestamp,
                        Symbol = record.Asset,
                        Quantity = record.AmountAsset!.Value + record.Fee!.Value,
                        Fee = record.Fee!.Value,
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
