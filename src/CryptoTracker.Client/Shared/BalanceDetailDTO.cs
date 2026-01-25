namespace CryptoTracker.Shared;

public record AssetBalanceDetailDTO(string Symbol,
                                    decimal Amount,
                                    decimal EuroValue,
                                    decimal RateEur,
                                    string? Slug);

public record WalletBalanceDTO(string WalletName,
                               decimal TotalEuroValue,
                               IList<AssetBalanceDetailDTO> Assets);
