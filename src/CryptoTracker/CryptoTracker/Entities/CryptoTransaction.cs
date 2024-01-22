namespace CryptoTracker.Entities
{
    public class CryptoTransaction
    {
        public int Id { get; set; }
        public DateTime DateTime { get; set; }
        public string TransactionType { get; set; }
        /// <summary>
        /// Wenn TransactionType: Send => Zielwallet / bei Receive => Quellwallet
        /// </summary>
        public string Wallet { get; set; }
        public string CoinSymbol { get; set; }
        /// <summary>
        /// Anzahl nach Gebührabzug
        /// </summary>
        public double Quantity { get; set; }

        /// <summary>
        /// In Coin als Währung (nur bei Send) / Bei Receive ist Fee immer 0
        /// </summary>
        public double Fee { get; set; }
        public string? TransactionId { get; set; }

        /// <summary>
        /// Wenn TransactionType: Send => Zieladresse / bei Receive => Quelladresse
        /// </summary>
        public string? Address { get; set; }
        public string? Network { get; set; }
        public string? Comment { get; set; }
    }
}
