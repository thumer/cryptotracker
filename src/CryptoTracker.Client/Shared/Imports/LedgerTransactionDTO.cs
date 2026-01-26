namespace CryptoTracker.Shared;

public record LedgerTransactionDTO
{
    public int WalletId { get; set; }
    public string Wallet { get; set; } = string.Empty;

    public DateTimeOffset Datum { get; set; }
    public string Typ { get; set; } = string.Empty;
    public string Coin { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal TransactionFee { get; set; }
    public string Kommentar { get; set; } = string.Empty;
}
