using System.Text.Json.Serialization;

namespace CryptoTracker.Shared;

public record OkxTradeDTO
{
    public int WalletId { get; set; }
    public string Wallet { get; set; } = string.Empty;

    public DateTimeOffset Date { get; set; }
    public string Pair { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public decimal Executed { get; set; }
}
