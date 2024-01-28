using CryptoTracker.Client.Pages;
using CryptoTracker.Entities;
using CryptoTracker.Import.Objects;
using CsvHelper;
using CsvHelper.Configuration;
using DevExpress.DirectX.Common.Direct2D;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CryptoTracker.Import
{
    public class BinanceTradeImporter : ImporterBase<BinanceTrade>
    {
        public BinanceTradeImporter(CryptoTrackerDbContext dbContext) 
            : base(dbContext)
        {
        }

        protected override CsvConfiguration CreateCsvConfiguration()
            => new CsvConfiguration(new System.Globalization.CultureInfo("en-US"));

        protected override void OnCsvReaderCreated(CsvReader reader)
        {
            base.OnCsvReaderCreated(reader);
            reader.Context.TypeConverterCache.AddConverter<DateTimeOffset>(new UtcDateTimeConverter());
        }

        protected override async Task OnImport(ImportArgs args, IEnumerable<BinanceTrade> records)
        {
            foreach (var record in records)
            {
                (decimal executed, string symbol1) = ParseNumberAndCurrency(record.Executed);
                var symbol2 = record.Pair.Substring(symbol1.Length);
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
                    Price = tradeType == TradeType.Sell ? record.Price : 1 / record.Price,
                    Quantity = tradeType == TradeType.Sell ? executed : amount,
                    Fee = feeSymbol == (tradeType == TradeType.Sell ? symbol1 : symbol2) ? fee : 0,
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

                sellTrade.OppositeTrade = buyTrade;
                buyTrade.OppositeTrade = sellTrade;
                DbContext.Add(sellTrade);
                DbContext.Add(buyTrade);
            }

            await DbContext.SaveChangesAsync();
        }

        private static (decimal Number, string Currency) ParseNumberAndCurrency(string input)
        {
            Match numberMatch = Regex.Match(input, @"[\d\.]+");
            Match currencyMatch = Regex.Match(input, @"[^\d\.]+");

            if (numberMatch.Success && currencyMatch.Success)
            {
                if (decimal.TryParse(numberMatch.Value, new CultureInfo("en-US"), out decimal number))
                {
                    string currency = currencyMatch.Value.Trim();
                    return (number, currency);
                }
            }

            throw new ArgumentException("Die Zeichenkette enthält keine gültige Zahl oder Währungseinheit.", nameof(input));
        }
    }
}