namespace CryptoTracker.Shared;

public record CoinRateDTO(string Symbol,
                          decimal? CurrentRateEur,
                          decimal? PreviousCloseRateEur,
                          DateTime? PreviousCloseDate,
                          string? Slug,
                          bool IsCurrentManual,
                          bool IsPreviousManual);

public record SetManualCoinRateRequest(string Symbol, DateTime Date, decimal PriceEur);
