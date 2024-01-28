using CryptoTracker.Entities;
using CryptoTracker.Import.Objects;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace CryptoTracker.Import
{
    public class MetamaskTradeImporter : ImporterBase<MetamaskTrade>
    {
        public MetamaskTradeImporter(CryptoTrackerDbContext dbContext) 
            : base(dbContext)
        {
        }

        protected override CsvConfiguration CreateCsvConfiguration()
            => new CsvConfiguration(new System.Globalization.CultureInfo("de-AT"))
            {
                Delimiter = ";",
            };

        protected override void OnCsvReaderCreated(CsvReader reader)
        {
            base.OnCsvReaderCreated(reader);
            reader.Context.TypeConverterCache.AddConverter<DateTimeOffset>(new UtcDateTimeConverter());
        }

        protected override async Task OnImport(ImportArgs args, IEnumerable<MetamaskTrade> records)
        {
            var trades = new List<(CryptoTrade sellTrade, CryptoTrade buyTrade)>();
            foreach (var record in records)
            {
                var splittedPair = record.Pair.Split('-');
                var symbol1 = splittedPair[0];
                var symbol2 = splittedPair[1];

                (decimal price, string priceSymbol) = ParseNumberAndCurrency(record.Price);
                (decimal executed, string executedSymbol) = ParseNumberAndCurrency(record.Executed);
                (decimal amount, string amountSymbol) = ParseNumberAndCurrency(record.Amount);
                (decimal fee, string feeSymbol) = ParseNumberAndCurrency(record.Fee);
                TradeType tradeType = record.Side == "BUY" ? TradeType.Buy : TradeType.Sell;

                var sellTrade = new CryptoTrade
                {
                    Wallet = args.Wallet,
                    DateTime = record.Date.LocalDateTime,
                    Symbol = tradeType == TradeType.Sell ? symbol1 : symbol2,
                    OpositeSymbol = tradeType == TradeType.Sell ? symbol2 : symbol1,
                    TradeType = TradeType.Sell,
                    Price = tradeType == TradeType.Sell ? price : 1 / price,
                    Quantity = tradeType == TradeType.Sell ? executed : amount,
                    ForeignFee = (feeSymbol != symbol1 && feeSymbol != symbol2) ? fee : 0,
                    ForeignFeeSymbol = (feeSymbol != symbol1 && feeSymbol != symbol2) ? feeSymbol : string.Empty,
                };
                var buyTrade = new CryptoTrade
                {
                    Wallet = args.Wallet,
                    DateTime = record.Date.LocalDateTime,
                    Symbol = tradeType == TradeType.Sell ? symbol2 : symbol1,
                    OpositeSymbol = tradeType == TradeType.Sell ? symbol1 : symbol2,
                    TradeType = TradeType.Buy,
                    Price = 1 / sellTrade.Price,
                    Quantity = tradeType == TradeType.Sell ? amount : executed,
                    Fee = feeSymbol == (tradeType == TradeType.Sell ? symbol2 : symbol1) ? fee : 0,
                    ForeignFee = 0,
                    ForeignFeeSymbol = string.Empty,
                };
                if (feeSymbol == (tradeType == TradeType.Sell ? symbol1 : symbol2))
                {
                    sellTrade.Fee = fee;
                    sellTrade.Quantity += fee;
                }
                else if (feeSymbol == (tradeType == TradeType.Sell ? symbol2 : symbol1))
                {
                    buyTrade.Fee = fee;
                    buyTrade.Quantity += fee;
                }

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
