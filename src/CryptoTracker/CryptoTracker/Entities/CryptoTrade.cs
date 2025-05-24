using CryptoTracker.Shared;

namespace CryptoTracker.Entities;

public enum TradeType
{
    Buy,
    Sell
}

public class CryptoTrade : IFlow
{
    public int Id { get; set; }
    public int WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;
    public DateTime DateTime { get; set; }

    /// <summary>
    /// Welcher Coin wurde gekauft/verkauft
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Mit welchen Coin wurde bezahlt / Betrag erhalten.
    /// </summary>
    public string OpositeSymbol { get; set; } = string.Empty;

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
    public string ForeignFeeSymbol {  get; set; } = string.Empty;

    /// <summary>
    /// Externe Referenz - z.B. TransactionId bei Kauf Fiat->Crypto
    /// </summary>
    public string? Referenz {  get; set; }
    public string? Comment { get; set; }

    public int? OppositeTradeId { get; set; }

    /// <summary>
    /// Gegenüberliegende Trade.
    /// </summary>
    public CryptoTrade? OppositeTrade { get; set; }

    FlowDirection IFlow.FlowDirection => TradeType switch
    {
        TradeType.Buy => FlowDirection.Inflow,
        TradeType.Sell => FlowDirection.Outflow,
        _ => throw new NotSupportedException()
    };

    decimal IFlow.FlowAmount => TradeType switch
    {
        TradeType.Buy => QuantityAfterFee,
        TradeType.Sell => Quantity,
        _ => throw new NotSupportedException()
    };

    string IFlow.SourceWallet => Wallet.Name;

    string IFlow.TargetWallet => Wallet.Name;

    FlowType IFlow.FlowType => FlowType.Trade;
}