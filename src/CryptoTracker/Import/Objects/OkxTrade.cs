﻿using CsvHelper.Configuration.Attributes;

namespace CryptoTracker.Import.Objects
{
    // csv-format: ; as separator, no "" for values, culture: de-AT (numbers, date), date in UTC
    // Example:
    // Date(UTC);Pair;Side;Price;Executed
    // 23.01.2018 20:25;INTETH;BUY;0,00084913 ETH;500

    public class OkxTrade : ICryptoCsvEntry
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
        /// Nach der Zahl steht das Währungssymbol. z.B. "0.1374ETH"
        /// </summary>
        public string Price {  get; set; } = string.Empty;

        /// <summary>
        /// Anzahl an {PrimarySymbol} die gekauft/verkauft wurden.
        /// </summary>
        public decimal Executed { get; set; }
    }
}
