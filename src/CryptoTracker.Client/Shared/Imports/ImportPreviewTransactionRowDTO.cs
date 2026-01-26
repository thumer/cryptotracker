namespace CryptoTracker.Shared;

public record ImportPreviewTransactionRowDTO(DateTimeOffset DateTime,
                                             string Type,
                                             string Coin,
                                             string? Slug,
                                             string? Network,
                                             string Amount,
                                             string Fee,
                                             string? Address,
                                             string? Comment,
                                             string? Source);
