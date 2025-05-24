namespace CryptoTracker.Entities.Import;

public class MetamaskTradeEntity
{
    public int Id { get; set; }
    public int WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;

    public DateTimeOffset Date { get; set; }
    public string Pair { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Executed { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string Fee { get; set; } = string.Empty;
    public string Tradingplatform { get; set; } = string.Empty;
}
