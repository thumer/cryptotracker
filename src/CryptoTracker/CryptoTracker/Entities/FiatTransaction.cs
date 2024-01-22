namespace CryptoTracker.Entities
{
    public class FiatTransaction
    {
        public Guid Id { get; set; }
        public DateTime DateTime { get; set; }
        public string TradeType { get; set; }
        public double Price { get; set; }
        public string CoinSymbol { get; set; }
        public string Wallet { get; set; }
        public double Quantity { get; set; }
        public double Fee { get; set; }
        public string? TransactionId { get; set; }
        public string? Comment { get; set; }
    }

}
