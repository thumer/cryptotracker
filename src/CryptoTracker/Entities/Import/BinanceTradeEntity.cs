namespace CryptoTracker.Entities.Import;

public class BinanceTradeEntity
{
    public int Id { get; set; }
    public int WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;

    public DateTimeOffset Date { get; set; }
    public string Pair { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Executed { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string Fee { get; set; } = string.Empty;
}
