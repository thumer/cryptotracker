using CsvHelper.Configuration.Attributes;

namespace CryptoTracker.Import.Objects
{
    // csv-format: , as separator, "" for values, culture: en-US (numbers, date)
    // Example:
    // "Date(UTC)","Pair","Side","Price","Executed","Amount","Fee"
    // "2024-01-22 19:37:27","ETHUSDT","SELL","2329.15","0.1374ETH","320.02521USDT","0.32002521USDT"

    public class BinanceTrade : ICryptoCsvEntry
    {
        [Name("Date(UTC)")]
        public DateTimeOffset Date { get; set; }

        /// <summary>
        /// "{PrimarySymbol}{SecondardSymbol}" z.B. "ETHUSDT"
        /// </summary>
        public string Pair {  get; set; } = string.Empty;

        /// <summary>
        /// SELL or BUY
        /// </summary>
        public string Side { get; set; } = string.Empty;

        /// <summary>
        /// Preis ausgehend von PrimarySymbol (1 {PrimarySymbol} kostet x {SecondardSymbol})
        /// </summary>
        public decimal Price {  get; set; }

        /// <summary>
        /// Anzahl an {PrimarySymbol} die gekauft/verkauft wurden.
        /// Nach der Zahl steht das Währungssymbol. z.B. "0.1374ETH"
        /// </summary>
        public string Executed { get; set; } = string.Empty;

        /// <summary>
        /// Anzahl an Coins in {SecondardSymbol} die im Gegenzug bezahlt bzw. erhalten wurden.
        /// Entspricht Wert vor Fee-Abzug.
        /// Nach der Zahl steht das Währungssymbol. z.B. "320.02521USDT"
        /// </summary>
        public string Amount { get; set; } = string.Empty;

        /// <summary>
        /// Anzahl von {SecondardSymbol} oder alternativ Währung, die beim Trade angefallen sind.
        /// 
        /// Nach der Zahl steht das Währungssymbol. z.B. "0.32002521USDT" oder "0.0079859100BNB"
        /// </summary>
        public string Fee { get; set; } = string.Empty;
    }
}
