using CryptoTracker.Entities;
using CryptoTracker.Shared;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Services;

public class BalanceService
{
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly IFinanceValueProvider _valueProvider;

    public BalanceService(CryptoTrackerDbContext dbContext, IFinanceValueProvider valueProvider)
    {
        _dbContext = dbContext;
        _valueProvider = valueProvider;
    }

    public async Task<IList<PlatformBalanceDTO>> GetBalances()
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

        var all = tradeFlows.Concat(txFlows)
            .GroupBy(x => new { x.Wallet, x.Symbol })
            .Select(g => new { g.Key.Wallet, g.Key.Symbol, Amount = g.Sum(x => x.Amount) })
            .ToList();

        var result = new List<PlatformBalanceDTO>();
        foreach (var walletGroup in all.GroupBy(x => x.Wallet))
        {
            var assets = new List<AssetBalanceDTO>();
            foreach (var asset in walletGroup)
            {
                var rate = await _valueProvider.GetCurrentEuroValueAsync(asset.Symbol);

                assets.Add(new AssetBalanceDTO(asset.Symbol, asset.Amount, asset.Amount * rate
                ));
            }

            result.Add(new PlatformBalanceDTO(walletGroup.Key, assets));
        }

        return result;
    }
}
