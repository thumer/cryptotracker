using CsvHelper.Configuration.Attributes;

namespace CryptoTracker.Import.Objects
{
    // csv-format: ; as separator, no "" for values, culture: de-AT (numbers, date)
    // Example:
    // Date(UTC); Coin;Network;Amount;TransactionFee;Address;TXID;Comment
    // 29.12.2017 21:10;BTC;BTC;0,57127657;0;1AyrgWt9nVoET4vgADmDUnVordkqgAwSLo;ec030407f019f959b664226a0dd145829850c3d9f6baa73fcd25634f848d1391;von bitcoin.de
    
    public class BinanceDeposit : ICryptoCsvEntry
    {
        [Ignore]
        public int Id { get; set; }
        [Name("Date(UTC)")]
        public DateTimeOffset Date { get; set; }

        public string Coin {  get; set; } = string.Empty;

        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Preis der erhalten wurden (Entspricht Preis nach Fee Abzug?)
        /// </summary>
        public decimal Amount {  get; set; }

        /// <summary>
        /// TransactionFee in der Coin-Symbol Währung
        /// </summary>
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