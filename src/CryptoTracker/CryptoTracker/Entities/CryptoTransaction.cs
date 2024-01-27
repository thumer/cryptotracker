namespace CryptoTracker.Entities
{
    public enum TransactionType
    {
        Send,
        Receive
    }

    public class CryptoTransaction
    {
        public int Id { get; set; }
        public string SourceWallet { get; set; }
        public string TargetWallet { get; set; }
        public DateTime DateTime { get; set; }
        public TransactionType TransactionType { get; set; }
        public string Symbol { get; set; }
        /// <summary>
        /// Anzahl vor Gebührabzug
        /// </summary>
        public double Quantity { get; set; }

        public double QuantityAfterFee => Quantity - Fee;

        /// <summary>
        /// In Coin als Währung (nur bei Send) / Bei Receive ist Fee immer 0
        /// </summary>
        public double Fee { get; set; }
        public string? TransactionId { get; set; }

        public int? OppositeTransactionId { get; set; }

        /// <summary>
        /// Falls es eine zusammengehörige Transaction gibt, dann haben diese die gleiche Guid.
        /// </summary>
        public CryptoTransaction? OppositeTransaction { get; set; }

        /// <summary>
        /// Wenn TransactionType: Send => Zieladresse / bei Receive => Quelladresse
        /// </summary>
        public string? Address { get; set; }
        public string? Network { get; set; }
        public string? Comment { get; set; }
    }
}
