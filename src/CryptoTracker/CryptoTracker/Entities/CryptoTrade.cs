namespace CryptoTracker.Entities
{
    public enum TradeType
    {
        Buy,
        Sell
    }

    public class CryptoTrade
    {
        public int Id { get; set; }
        public string Wallet { get; set; }
        public DateTime DateTime { get; set; }

        /// <summary>
        /// Welcher Coin wurde gekauft/verkauft
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// Mit welchen Coin wurde bezahlt / Betrag erhalten.
        /// </summary>
        public string OpositeSymbol { get; set; }

        public TradeType TradeType { get; set; }

        /// <summary>
        /// Preis ausgehend von Symbol (1 {Symbol} kostet x  {OpositeSymbol})
        /// </summary>
        public decimal Price { get; set; }
        /// <summary>
        /// Anzahl vor Gebührabzug
        /// </summary>
        public decimal Quantity { get; set; }

        public decimal QuantityAfterFee => Quantity - Fee;

        /// <summary>
        /// In Anzahl in der Währungseinheit
        /// </summary>
        public decimal Fee { get; set; }
        public decimal ForeignFee {  get; set; }
        public string ForeignFeeSymbol {  get; set; }
        public string? Comment { get; set; }

        public int? OppositeTradeId { get; set; }

        /// <summary>
        /// Gegenüberliegende Trade.
        /// </summary>
        public CryptoTrade? OppositeTrade { get; set; }
    }
}