using CsvHelper.Configuration.Attributes;

namespace CryptoTracker.Import.Objects
{
    // csv-format: ; as separator, no "" for values, culture: de-AT (numbers, date)
    // Example:
    // Date(UTC); Coin;Network;Amount;TransactionFee;Address;TXID;Comment
    // 01.02.2018 19:46;BTC;;0,026;0,001;33tRpdnG68JKadrzPbTD9VingnekiasKsQ;62ee20deb7742fa4c0a66b1e2bf84d5bd72a43d38f8d98a9ae02ecdc67899d1d;an okx.com

    public class BinanceWithdrawal : ICryptoCsvEntry
    {
        [Ignore]
        public int Id { get; set; }
        [Name("Date(UTC)")]
        public DateTimeOffset Date { get; set; }

        public string Coin {  get; set; } = string.Empty;

        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Preis die ausbezahlt wurden. Entspricht Preis nach Fee Abzug.
        /// </summary>
        public decimal Amount {  get; set; }

        /// <summary>
        /// TransactionFee in der Coin-Symbol Währung
        /// </summary>
        [Name("Fee")]
        public decimal TransactionFee { get; set; }
        
        /// <summary>
        /// Zieladresse
        /// </summary>
        public string Address {  get; set; } = string.Empty;

        /// <summary>
        /// Binance TransactionId
        /// </summary>
        public string TXID { get; set; } = string.Empty;

        public string Comment {  get; set; } = string.Empty;
    }
}
