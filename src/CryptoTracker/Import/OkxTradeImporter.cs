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
                if (string.IsNullOrEmpty(symbol2))
                {
                    symbol2 = record.Pair.Substring(record.Pair.Length - 4);
                }
                var symbol1 = record.Pair.Substring(0, record.Pair.Length - symbol2.Length);
                TradeType tradeType = record.Side == "BUY" ? TradeType.Buy : TradeType.Sell;

                decimal amount = record.Executed * price;
                var sellTrade = new CryptoTrade
                {
                    WalletId = args.Wallet.Id,
                    DateTime = record.Date.LocalDateTime,
                    Symbol = tradeType == TradeType.Sell ? symbol1 : symbol2,
                    OpositeSymbol = tradeType == TradeType.Sell ? symbol2 : symbol1,
                    TradeType = TradeType.Sell,
                    Price = tradeType == TradeType.Sell ? price : 1 / price,
                    Quantity = tradeType == TradeType.Sell ? record.Executed : amount,
                    Fee = 0,
                    ForeignFee = 0,
                    ForeignFeeSymbol = string.Empty,
                };
                var buyTrade = new CryptoTrade
                {
                    WalletId = args.Wallet.Id,
                    DateTime = record.Date.LocalDateTime,
                    Symbol = tradeType == TradeType.Sell ? symbol2 : symbol1,
                    OpositeSymbol = tradeType == TradeType.Sell ? symbol1 : symbol2,
                    TradeType = TradeType.Buy,
                    Price = tradeType == TradeType.Sell ? 1 / price : price,
                    Quantity = tradeType == TradeType.Sell ? amount : record.Executed,
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
            string numberStr;
            string currencyStr;
            if (splitIndex != -1)
            {
                numberStr = input.Substring(0, splitIndex);
                currencyStr = input.Substring(splitIndex + 1);
            }
            else
            {
                int i = 0;
                while (i < input.Length && (char.IsDigit(input[i]) || input[i] == ',' || input[i] == '.' || input[i] == '-' || input[i] == 'E' || input[i] == 'e' || input[i] == '+'))
                {
                    i++;
                }
                if (i == 0)
                {
                    throw new ArgumentException("Die Zeichenkette enthält keine gültige Zahl oder Währungseinheit.", nameof(input));
                }
                numberStr = input.Substring(0, i);
                currencyStr = i == input.Length ? string.Empty : input.Substring(i);
            }

            var number = decimal.Parse(numberStr, NumberStyles.Float, new CultureInfo("de-AT"));
            return (number, currencyStr);
        }
    }
}