using CryptoTracker.Shared;

namespace CryptoTracker.Services;

public class OverviewService
{
    private readonly BalanceService _balanceService;

    public OverviewService(BalanceService balanceService)
    {
        _balanceService = balanceService;
    }

    public async Task<OverviewSummaryDTO> GetOverviewAsync()
    {
        var walletBalances = await _balanceService.GetWalletBalancesAsync();
        var allAssets = walletBalances.SelectMany(w => w.Assets).ToList();

        var topCoins = allAssets
            .GroupBy(a => a.Symbol)
            .Select(g =>
            {
                var amount = g.Sum(x => x.Amount);
                var euroValue = RoundEuro(g.Sum(x => x.EuroValue));
                var rate = RoundEuro(g.First().RateEur);
                var slug = g.First().Slug;
                return new CoinSummaryDTO(g.Key, amount, euroValue, rate, slug);
            })
            .OrderByDescending(c => c.EuroValue)
            .Take(3)
            .ToList();

        var totalEuro = RoundEuro(walletBalances.Sum(w => w.TotalEuroValue));
        var coinCount = allAssets.Select(a => a.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var walletSummaries = walletBalances
            .Select(w => new WalletSummaryDTO(w.WalletName, RoundEuro(w.TotalEuroValue), w.Assets.Count))
            .ToList();

        return new OverviewSummaryDTO(totalEuro, coinCount, topCoins, walletSummaries);
    }

    private static decimal RoundEuro(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
