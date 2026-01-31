namespace CryptoTracker.Shared;

/// <summary>
/// DTO für ein Asset-Lot (Tranche).
/// </summary>
public record LotDTO(
    int Id,
    string Symbol,
    int WalletId,
    string WalletName,
    decimal RemainingQuantity,
    decimal OriginalQuantity,
    DateTimeOffset AcquisitionDate,
    decimal AcquisitionPriceEur,
    decimal TotalAcquisitionCostEur,
    string AcquisitionType,
    bool IsAltbestand,
    bool IsFullyConsumed,
    string? Note,
    int? ParentLotId,
    int? SourceTradeId,
    int? SourceTransactionId,
    bool IsFlowComplete = true,
    string? FlowIncompleteReason = null);

/// <summary>
/// DTO für Lot-Zusammenfassung pro Symbol.
/// </summary>
public record LotSummaryDTO(
    string Symbol,
    decimal TotalQuantity,
    decimal AltbestandQuantity,
    decimal NeubestandQuantity,
    decimal TotalAcquisitionCostEur,
    decimal AverageAcquisitionPrice,
    int LotCount);

/// <summary>
/// DTO für eine Lot-Zuordnung (bei Transfer/Verkauf).
/// </summary>
public record LotAllocationDTO(
    int LotId,
    decimal Quantity);

/// <summary>
/// Request für Transfer mit Lot-Zuordnung.
/// </summary>
public record TransferLotsRequest(
    int SendTransactionId,
    int ReceiveTransactionId,
    IList<LotAllocationDTO> Allocations);

/// <summary>
/// Request für Verkauf mit Lot-Zuordnung.
/// </summary>
public record SellLotsRequest(
    int TradeId,
    IList<LotAllocationDTO> Allocations,
    decimal SalePriceEurPerUnit);

/// <summary>
/// Request für manuelle Lot-Erstellung.
/// </summary>
public record CreateManualLotRequest(
    string Symbol,
    int WalletId,
    decimal Quantity,
    DateTimeOffset AcquisitionDate,
    decimal AcquisitionPriceEur,
    string AcquisitionType,
    int? SourceTransactionId,
    string? Note);

/// <summary>
/// Ergebnis eines Verkaufs.
/// </summary>
public record SaleResultDTO(
    decimal TotalQuantity,
    decimal TotalAcquisitionCost,
    decimal TotalSaleProceeds,
    decimal TotalRealizedGain,
    decimal TaxFreeGain,
    decimal TaxFreeQuantity,
    decimal TaxableGain,
    decimal TaxableQuantity,
    decimal EstimatedKESt);

/// <summary>
/// FIFO-Vorschlag für eine bestimmte Menge.
/// </summary>
public record FifoSuggestionDTO(
    IList<LotAllocationDTO> Allocations,
    decimal TotalAllocated,
    decimal MissingQuantity,
    bool IsComplete);

/// <summary>
/// Transaktionen/Trades die Lot-Zuordnung benötigen.
/// </summary>
public record PendingLotAssignmentDTO(
    string Type, // "Transaction" oder "Trade"
    int Id,
    DateTimeOffset DateTime,
    string Symbol,
    decimal Quantity,
    string WalletName,
    int WalletId,
    string? Direction, // "Receive" für Transactions, "Sell" für Trades
    string? OppositeWalletName,
    string? Comment); // Kommentar der Transaktion für Regel-Matching

/// <summary>
/// Request für Lot-Generierung aus bestehenden Daten.
/// </summary>
public record GenerateLotsRequest(
    bool FromTrades = true,
    bool FromTransactions = false);

/// <summary>
/// Ergebnis der Lot-Generierung.
/// </summary>
public record GenerateLotsResultDTO(
    int LotsCreated,
    int Errors);

/// <summary>
/// DTO für Flow-Validierungsergebnis.
/// </summary>
public record LotFlowValidationDTO(
    int LotId,
    string Symbol,
    decimal Quantity,
    DateTimeOffset AcquisitionDate,
    bool IsComplete,
    string? IncompleteReason,
    IList<LotFlowStepDTO> FlowChain,
    decimal EffectiveQuantity);

/// <summary>
/// DTO für einen Schritt in der Flow-Kette.
/// </summary>
public record LotFlowStepDTO(
    int LotId,
    string Symbol,
    decimal Quantity,
    string Type,
    int? TradeId,
    int? TransactionId,
    DateTimeOffset DateTime);

/// <summary>
/// Request für Swap-Transformation.
/// </summary>
public record TransformLotsViaSwapRequest(
    int SellTradeId,
    int BuyTradeId,
    IList<LotAllocationDTO> SourceAllocations,
    decimal ResultingQuantity);

/// <summary>
/// Ergebnis der Flow-Revalidierung.
/// </summary>
public record RevalidateFlowResultDTO(
    int UpdatedCount,
    int CompleteCount,
    int IncompleteCount);

// ============================================
// DTOs für interaktives Lot-Linking
// ============================================

/// <summary>
/// Statistiken für Lot-Zuordnungen.
/// </summary>
public record LotLinkingStatisticsDTO
{
    public int TotalPendingAssignments { get; init; }
    public int PendingReceiveTransactions { get; init; }
    public int PendingSellTrades { get; init; }
    public int CompletedAssignments { get; init; }
    public int LotsCreated { get; init; }
}

/// <summary>
/// Session-State für interaktives Lot-Linking.
/// </summary>
public record InteractiveLotLinkingSessionDTO
{
    public string SessionId { get; init; } = "";
    public bool IsActive { get; init; }
    public int ProcessedCount { get; init; }
    public int TotalCount { get; init; }
    public int AssignedCount { get; init; }
    public int CreatedLotsCount { get; init; }
    public int SkippedCount { get; init; }
    public string? CurrentQuestionId { get; init; }
    public string? CurrentQuestion { get; init; }
    public PendingLotAssignmentDTO? CurrentAssignment { get; init; }
    public IList<string>? CurrentOptions { get; init; }
}

/// <summary>
/// Live-Event vom Lot-Linking Agent.
/// </summary>
public record LotLinkingEventDTO
{
    public string EventType { get; init; } = ""; // "assigned", "lot_created", "question", "progress", "error", "rule_learned"
    public string Message { get; init; } = "";
    public PendingLotAssignmentDTO? Assignment { get; init; }
    public LotDTO? CreatedLot { get; init; }
    public string? QuestionId { get; init; }
    public IList<string>? Options { get; init; }
    public IList<LotOptionDTO>? LotOptions { get; init; }
    public int ProcessedCount { get; init; }
    public int TotalCount { get; init; }
}

/// <summary>
/// Option für Lot-Auswahl bei Fragen.
/// </summary>
public record LotOptionDTO
{
    public int LotId { get; init; }
    public string DisplayText { get; init; } = "";
    public decimal AvailableQuantity { get; init; }
    public DateTimeOffset AcquisitionDate { get; init; }
    public decimal AcquisitionPriceEur { get; init; }
    public bool IsAltbestand { get; init; }
    public string WalletName { get; init; } = "";
}

/// <summary>
/// Antwort vom User auf Lot-Linking Frage.
/// </summary>
public record LotLinkingUserResponseDTO
{
    public string QuestionId { get; init; } = "";
    public string Response { get; init; } = "";
    public bool ShouldRemember { get; init; } = true;
    public IList<LotAllocationDTO>? LotAllocations { get; init; }
    public string? CustomText { get; init; }
    // Für manuelle Lot-Erstellung
    public string? AcquisitionType { get; init; }
    public decimal? AcquisitionPriceEur { get; init; }
    public string? Note { get; init; }
}

/// <summary>
/// Gelernte Regel für Lot-Zuordnung.
/// </summary>
public record LotLinkingRuleDTO
{
    public string Id { get; init; } = "";
    public string RuleType { get; init; } = ""; // "comment_pattern", "symbol_pattern", "wallet_pattern"
    public string Pattern { get; init; } = "";
    public string Action { get; init; } = ""; // "create_lot_airdrop", "create_lot_staking", "create_lot_mining", "fifo", "skip"
    public string Description { get; init; } = "";
    public int TimesApplied { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
