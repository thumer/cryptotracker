namespace CryptoTracker.Shared;

public record ImportPreviewTradeRowDTO(DateTimeOffset DateTime,
                                       string Pair,
                                       string Side,
                                       string Price,
                                       string Executed,
                                       string Amount,
                                       string Fee,
                                       string? Source);
