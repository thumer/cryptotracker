namespace CryptoTracker.Shared;

public record WalletSummaryDTO(string WalletName, decimal EuroValue, int AssetCount);

public record CoinSummaryDTO(string Symbol,
                            decimal Amount,
                            decimal EuroValue,
                            decimal RateEur,
                            string? Slug);

public record OverviewSummaryDTO(decimal TotalEuroValue,
                                 int CoinCount,
                                 IList<CoinSummaryDTO> TopCoins,
                                 IList<WalletSummaryDTO> Wallets);
