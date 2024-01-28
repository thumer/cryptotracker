using CsvHelper.Configuration.Attributes;

namespace CryptoTracker.Import.Objects
{
    // csv-format: ; as separator, no "" for values, culture: de-AT (numbers, date), date in UTC
    // Example:
    // Date(UTC);Pair;Side;Price;Executed;Amount;Fee;Tradingplatform
    // 02.07.2021;BabyDo-ETH;BUY;1,0142E-12 ETH;1,8E+11 BabyDo;0,17 ETH;2E+11 BabyDo;pancakeswap.finance

    public class MetamaskTrade : ICryptoCsvEntry
    {
        [Name("Date(UTC)")]
        public DateTimeOffset Date { get; set; }

        /// <summary>
        /// "{PrimarySymbol}{SecondardSymbol}" z.B. "ETHUSDT"
        /// </summary>
        public string Pair {  get; set; }

        /// <summary>
        /// SELL or BUY
        /// </summary>
        public string Side { get; set; }

        /// <summary>
        /// Preis ausgehend von PrimarySymbol (1 {PrimarySymbol} kostet x {SecondardSymbol})
        /// </summary>
        public decimal Price {  get; set; }

        /// <summary>
        /// Anzahl an {PrimarySymbol} die gekauft/verkauft wurden.
        /// Nach der Zahl steht das Währungssymbol. z.B. "0.1374ETH"
        /// </summary>
        public string Executed { get; set; }

        /// <summary>
        /// Anzahl an Coins in {SecondardSymbol} die im Gegenzug bezahlt bzw. erhalten wurden.
        /// Entspricht Wert vor Fee-Abzug.
        /// Nach der Zahl steht das Währungssymbol. z.B. "320.02521USDT"
        /// </summary>
        public string Amount { get; set; }

        /// <summary>
        /// Anzahl von {SecondardSymbol} oder alternativ Währung, die beim Trade angefallen sind.
        /// 
        /// Nach der Zahl steht das Währungssymbol. z.B. "0.32002521USDT" oder "0.0079859100BNB"
        /// </summary>
        public string Fee { get; set; }

        public string TradingPlatform { get; set; }
    }
}
