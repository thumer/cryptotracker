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
    public int WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;
    public DateTimeOffset DateTime { get; set; }
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
    public int? OppositeWalletId { get; set; }
    public Wallet? OppositeWallet { get; set; }

    public string? TransactionId { get; set; }
    /// <summary>
    /// Wenn TransactionType: Send => Zieladresse / bei Receive => Quelladresse
    /// </summary>
    public string? Address { get; set; }
    public string? Network { get; set; }
    public string? Comment { get; set; }

    // === Lot-Tracking ===

    /// <summary>
    /// Bei Send: Welche Lots wurden für diese Transaktion verwendet?
    /// Bei Receive: Welches Lot wurde erstellt?
    /// </summary>
    public ICollection<LotMovement> LotMovements { get; set; } = new List<LotMovement>();

    /// <summary>
    /// Bei Receive: Das erstellte Lot (falls zugeordnet)
    /// </summary>
    public int? ResultingLotId { get; set; }
    public AssetLot? ResultingLot { get; set; }

    /// <summary>
    /// Wurde die Lot-Zuordnung für diese Transaktion bestätigt?
    /// </summary>
    public bool LotAssignmentConfirmed { get; set; }

    /// <summary>
    /// Benötigt manuelle Lot-Zuordnung?
    /// Bei Receive ohne verknüpfte Gegentransaktion muss Herkunft dokumentiert werden.
    /// </summary>
    public bool RequiresLotAssignment =>
        TransactionType == TransactionType.Receive &&
        OppositeTransactionId == null &&
        !LotAssignmentConfirmed;

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
        TransactionType.Receive => OppositeWallet?.Name,
        TransactionType.Send => Wallet.Name,
        _ => throw new NotSupportedException()
    };

    string? IFlow.TargetWallet => TransactionType switch
    {
        TransactionType.Receive => Wallet.Name,
        TransactionType.Send => OppositeWallet?.Name,
        _ => throw new NotSupportedException()
    };

    FlowType IFlow.FlowType => FlowType.Transaction;
}