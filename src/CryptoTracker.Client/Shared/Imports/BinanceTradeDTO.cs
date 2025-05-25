namespace CryptoTracker.Shared;

public record BinanceTradeDTO
{
    public DateTimeOffset Date { get; set; }
    public string Pair { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Executed { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string Fee { get; set; } = string.Empty;
}
