namespace CryptoTracker.Entities.Import;

public class BinanceDepositEntity
{
    public int Id { get; set; }
    public int WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;

    public DateTimeOffset Date { get; set; }
    public string Coin { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal TransactionFee { get; set; }
    public string Address { get; set; } = string.Empty;
    public string TXID { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}
