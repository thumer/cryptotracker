namespace CryptoTracker.Shared;

public record AssetFlowLineDTO(
    DateTimeOffset DateTime,
    string? TransactionId,
    int? TradeId,
    string? SourceWallet,
    string? TargetWallet,
    string Symbol,
    decimal Amount,
    string? OppositeSymbol,
    decimal? OppositeAmount,
    decimal? Price);
