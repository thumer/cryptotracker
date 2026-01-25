using CryptoTracker.Entities;
using CryptoTracker.Shared;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Services;

public class BalanceService
{
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly CoinRateService _coinRateService;

    public BalanceService(CryptoTrackerDbContext dbContext, CoinRateService coinRateService)
    {
        _dbContext = dbContext;
        _coinRateService = coinRateService;
    }

    public async Task<IList<PlatformBalanceDTO>> GetBalances()
    {
        var balances = await GetWalletBalancesAsync();
        return balances
            .Select(b => new PlatformBalanceDTO(b.WalletName, b.Assets.Select(a => new AssetBalanceDTO(a.Symbol, a.Amount, a.EuroValue)).ToList()))
            .ToList();
    }

    public async Task<IList<WalletBalanceDTO>> GetWalletBalancesAsync()
    {
        var walletNames = await _dbContext.Wallets
            .OrderBy(w => w.Name)
            .Select(w => w.Name)
            .ToListAsync();

        var aggregated = await GetAggregatedAmountsAsync();
        var symbols = aggregated.Select(a => a.Symbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rateLookup = await _coinRateService.GetCurrentRatesAsync(symbols);
        var slugLookup = await _coinRateService.GetSlugsAsync(symbols);

        var result = new List<WalletBalanceDTO>();
        foreach (var walletName in walletNames)
        {
            var assets = aggregated
                .Where(a => a.Wallet.Equals(walletName, StringComparison.OrdinalIgnoreCase))
                .Select(a =>
                {
                    var rate = rateLookup.TryGetValue(a.Symbol, out var r) ? r.rate : 0m;
                    var slug = slugLookup.TryGetValue(a.Symbol, out var s) ? s : null;
                    var euroValue = a.Amount * rate;
                    return new AssetBalanceDetailDTO(a.Symbol, a.Amount, euroValue, rate, slug);
                })
                .Where(a => a.Amount != 0)
                .OrderByDescending(a => a.EuroValue)
                .ToList();

            var total = assets.Sum(a => a.EuroValue);
            result.Add(new WalletBalanceDTO(walletName, total, assets));
        }

        return result;
    }

    public async Task<WalletBalanceDTO?> GetWalletBalanceAsync(string walletName)
    {
        if (string.IsNullOrWhiteSpace(walletName))
            return null;

        var all = await GetWalletBalancesAsync();
        return all.FirstOrDefault(b => b.WalletName.Equals(walletName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IList<(string Wallet, string Symbol, decimal Amount)>> GetAggregatedAmountsAsync()
    {
        var tradeFlows = await _dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .Select(t => new
            {
                Wallet = t.Wallet.Name,
                t.Symbol,
                Amount = t.TradeType == TradeType.Buy ? t.QuantityAfterFee : -t.Quantity
            })
            .ToListAsync();

        var txFlows = await _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Select(t => new
            {
                Wallet = t.Wallet.Name,
                t.Symbol,
                Amount = t.TransactionType == TransactionType.Receive ? t.QuantityAfterFee : -t.Quantity
            })
            .ToListAsync();

        return tradeFlows.Concat(txFlows)
            .GroupBy(x => new { x.Wallet, x.Symbol })
            .Select(g => (g.Key.Wallet, g.Key.Symbol, Amount: g.Sum(x => x.Amount)))
            .ToList();
    }
}
