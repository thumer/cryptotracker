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
    int? SourceTransactionId);

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
    string? Direction, // "Receive" für Transactions, "Sell" für Trades
    string? OppositeWalletName);

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
