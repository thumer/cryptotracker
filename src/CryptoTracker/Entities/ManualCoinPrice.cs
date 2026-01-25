namespace CryptoTracker.Entities;

public class ManualCoinPrice
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal PriceEur { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
