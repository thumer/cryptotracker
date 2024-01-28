using CryptoTracker.Entities;
using CryptoTracker.Import.Objects;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace CryptoTracker.Import
{
    public class OkxTradeImporter : ImporterBase<OkxTrade>
    {
        public OkxTradeImporter(CryptoTrackerDbContext dbContext) 
            : base(dbContext)
        {
        }

        protected override CsvConfiguration CreateCsvConfiguration()
            => new CsvConfiguration(new CultureInfo("de-AT"))
            {
                Delimiter = ";",
            };

        protected override void OnCsvReaderCreated(CsvReader reader)
        {
            base.OnCsvReaderCreated(reader);
            reader.Context.TypeConverterCache.AddConverter<DateTimeOffset>(new UtcDateTimeConverter());
        }

        protected override async Task OnImport(ImportArgs args, IEnumerable<OkxTrade> records)
        {
            var trades = new List<(CryptoTrade sellTrade, CryptoTrade buyTrade)>();
            foreach (var record in records)
            {
                (decimal price, string symbol2) = ParseNumberAndCurrency(record.Price); 
                var symbol1 = record.Pair.Substring(0, record.Pair.Length - symbol2.Length);
                TradeType tradeType = record.Side == "BUY" ? TradeType.Buy : TradeType.Sell;

                var sellTrade = new CryptoTrade
                {
                    Wallet = args.Wallet,
                    DateTime = record.Date.LocalDateTime,
                    Symbol = tradeType == TradeType.Sell ? symbol1 : symbol2,
                    OpositeSymbol = tradeType == TradeType.Sell ? symbol2 : symbol1,
                    TradeType = TradeType.Sell,
                    Price = tradeType == TradeType.Sell ? price : 1 / price,
                    Quantity = tradeType == TradeType.Sell ? record.Executed : 1 / record.Executed,
                    Fee = 0,
                    ForeignFee = 0,
                    ForeignFeeSymbol = string.Empty,
                };
                var buyTrade = new CryptoTrade
                {
                    Wallet = args.Wallet,
                    DateTime = record.Date.LocalDateTime,
                    Symbol = tradeType == TradeType.Sell ? symbol2 : symbol1,
                    OpositeSymbol = tradeType == TradeType.Sell ? symbol1 : symbol2,
                    TradeType = TradeType.Buy,
                    Price = 1 / sellTrade.Price,
                    Quantity = tradeType == TradeType.Buy ? record.Executed : 1 / record.Executed,
                    Fee = 0,
                    ForeignFee = 0,
                    ForeignFeeSymbol = string.Empty,
                };

                DbContext.Add(sellTrade);
                DbContext.Add(buyTrade);
                trades.Add((sellTrade, buyTrade));
            }

            await DbContext.SaveChangesAsync();

            foreach (var pair in trades)
            {
                pair.sellTrade.OppositeTrade = pair.buyTrade;
                pair.buyTrade.OppositeTrade = pair.sellTrade;
            }

            await DbContext.SaveChangesAsync();
        }

        private static (decimal Number, string Currency) ParseNumberAndCurrency(string input)
        {
            int splitIndex = input.IndexOf(' ');
            if (splitIndex != -1)
            {
                string numberStr = input.Substring(0, splitIndex);
                string currencyStr = input.Substring(splitIndex + 1);
                var number = decimal.Parse(numberStr, NumberStyles.Float);
                return (number, currencyStr);
            }

            throw new ArgumentException("Die Zeichenkette enthält keine gültige Zahl oder Währungseinheit.", nameof(input));
        }
    }
}