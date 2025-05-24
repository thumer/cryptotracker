namespace CryptoTracker.Entities.Import;

public class MetamaskTransactionEntity
{
    public int Id { get; set; }
    public int WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;

    public DateTimeOffset Datum { get; set; }
    public string Typ { get; set; } = string.Empty;
    public string Coin { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal TransactionFee { get; set; }
    public string Kommentar { get; set; } = string.Empty;
}
