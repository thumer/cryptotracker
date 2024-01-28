using CsvHelper.Configuration.Attributes;
using DevExpress.Pdf.Native.BouncyCastle.Asn1.X509;

namespace CryptoTracker.Import.Objects
{
    // csv-format: ; as separator, no "" for values, culture: de-AT (numbers, date), date in UTC
    // Example:
    // Datum;Typ;Coin;Network;Amount;TransactionFee;Kommentar
    // 02.07.2021 20:38;Eingang;ETH;BSC;0,209932;0,000068;von binance.com

    public class MetamaskTransaction : ICryptoCsvEntry
    {
        /// <summary>
        /// Datum in ISO 8601 und CET
        /// </summary>
        public DateTimeOffset Datum { get; set; }

        /// <summary>
        /// Eingang, Ausgang
        /// </summary>
        public string Typ { get; set; }

        /// <summary>
        /// Symbol der Cryptowährung
        /// </summary>
        public string Coin { get; set; }

        /// <summary>
        /// Netzwerk über das die Coins übertragen wurden
        /// </summary>
        public string Network { get; set; }

        /// <summary>
        /// Anzahl der Coins vor Gebührenabzug
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Preis die ausbezahlt wurden. Entspricht Preis nach Fee Abzug. 
        /// </summary>
        public decimal TransactionFee { get; set; }

        public string Kommentar { get; set; }
    }
}
