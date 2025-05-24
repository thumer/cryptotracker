using CryptoTracker.Shared;

namespace CryptoTracker.Entities;

public enum TransactionType
{
    Send,
    Receive
}

public class CryptoTransaction : IFlow
{
    public int Id { get; set; }

    /// <summary>
    /// Wenn TransactionType: Receive => Zielwallet / bei Send => Quellwallet
    /// </summary>
    public string Wallet { get; set; } = string.Empty;
    public DateTime DateTime { get; set; }
    public TransactionType TransactionType { get; set; }
    public string Symbol { get; set; } = string.Empty;
    /// <summary>
    /// Anzahl vor Gebührabzug
    /// </summary>
    public decimal Quantity { get; set; }

    public decimal QuantityAfterFee => Quantity - Fee;

    /// <summary>
    /// In Coin als Währung (nur bei Send) / Bei Receive ist Fee immer 0
    /// </summary>
    public decimal Fee { get; set; }

    public int? OppositeTransactionId { get; set; }

    /// <summary>
    /// Falls es eine zusammengehörige Transaction gibt, dann haben diese die gleiche Guid.
    /// </summary>
    public CryptoTransaction? OppositeTransaction { get; set; }

    /// <summary>
    /// OppositeTransaction.Wallet (ist zwar redundant - vereinfacht jedoch die Abfragen).
    /// </summary>
    public string? OppositeWallet { get; set; }

    public string? TransactionId { get; set; }
    /// <summary>
    /// Wenn TransactionType: Send => Zieladresse / bei Receive => Quelladresse
    /// </summary>
    public string? Address { get; set; }
    public string? Network { get; set; }
    public string? Comment { get; set; }

    FlowDirection IFlow.FlowDirection => TransactionType switch
    {
        TransactionType.Receive => FlowDirection.Inflow,
        TransactionType.Send => FlowDirection.Outflow,
        _ => throw new NotSupportedException()
    };

    decimal IFlow.FlowAmount => TransactionType switch
    {
        TransactionType.Receive => QuantityAfterFee,
        TransactionType.Send => Quantity,
        _ => throw new NotSupportedException()
    };

    string? IFlow.SourceWallet => TransactionType switch
    {
        TransactionType.Receive => OppositeWallet,
        TransactionType.Send => Wallet,
        _ => throw new NotSupportedException()
    };

    string? IFlow.TargetWallet => TransactionType switch
    {
        TransactionType.Receive => Wallet,
        TransactionType.Send => OppositeWallet,
        _ => throw new NotSupportedException()
    };

    FlowType IFlow.FlowType => FlowType.Transaction;
}