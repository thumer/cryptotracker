namespace CryptoTracker.Entities.Import;

public class BitpandaTransactionEntity
{
    public int Id { get; set; }
    public int WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;

    public string TransactionId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string InOut { get; set; } = string.Empty;
    public decimal? AmountFiat { get; set; }
    public string Fiat { get; set; } = string.Empty;
    public decimal? AmountAsset { get; set; }
    public string Asset { get; set; } = string.Empty;
    public decimal? AssetMarketPrice { get; set; }
    public string AssetMarketPriceCurrency { get; set; } = string.Empty;
    public string AssetClass { get; set; } = string.Empty;
    public int? ProductID { get; set; }
    public decimal? Fee { get; set; }
    public string FeeAsset { get; set; } = string.Empty;
    public decimal? Spread { get; set; }
    public string SpreadCurrency { get; set; } = string.Empty;
    public decimal? TaxFiat { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}
