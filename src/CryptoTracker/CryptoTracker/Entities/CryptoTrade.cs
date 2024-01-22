namespace CryptoTracker.Entities
{

    public class CryptoTrade
    {
        public int Id { get; set; }
        public DateTime DateTime { get; set; }
        public string CoinSymbolFrom { get; set; }
        public string CoinSymbolTo { get; set; }

        /// <summary>
        /// Anzahl im Ziel Coin
        /// </summary>
        public double Price { get; set; }

        /// <summary>
        /// Anzahl im Ziel Coin; Anzahl vor Gebühr
        /// </summary>
        public double Quantity { get; set; }
        public string Wallet { get; set; }
        public double Fee { get; set; }
        public string? Comment { get; set; }
    }
}
