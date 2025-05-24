namespace CryptoTracker.Entities.Import;

public class MetamaskTransactionEntity
{
    public int Id { get; set; }
    public int WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;

    public DateTimeOffset Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string ValueInUSD { get; set; } = string.Empty;
    public string TransactionFee { get; set; } = string.Empty;
    public string TransactionFeeInUSD { get; set; } = string.Empty;
    public string GasPrice { get; set; } = string.Empty;
    public string GasLimit { get; set; } = string.Empty;
}
