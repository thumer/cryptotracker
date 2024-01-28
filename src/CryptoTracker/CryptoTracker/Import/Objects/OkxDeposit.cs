using CsvHelper.Configuration.Attributes;

namespace CryptoTracker.Import.Objects
{
    // csv-format: ; as separator, no "" for values, culture: de-AT (numbers, date)
    // Example:
    // Date(UTC);Pair;Side;Price;Executed
    // 23.01.2018 20:25;INTETH;BUY;0,00084913 ETH;500

    public class OkxDeposit : ICryptoCsvEntry
    {
        [Name("Date(UTC)")]
        public DateTimeOffset Date { get; set; }

        public string Coin {  get; set; }

        public string Network { get; set; }

        /// <summary>
        /// Preis die ausbezahlt wurden. Entspricht Preis nach Fee Abzug.
        /// </summary>
        public decimal Amount {  get; set; }
        
        /// <summary>
        /// Zieladresse
        /// </summary>
        public string Address {  get; set; }

        public string Kommentar {  get; set; }
    }
}
