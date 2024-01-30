using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Services;

public class WalletService
{
    private readonly CryptoTrackerDbContext _dbContext;

    public WalletService(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IList<(string wallet, string[] symbols)>> GetWalletAndSymbols()
    {
        var trades = await _dbContext.CryptoTrades.Select(t => new { t.Wallet, t.Symbol }).Distinct().ToListAsync();
        var transactions = await _dbContext.CryptoTransactions.Select(t => new { t.Wallet, t.Symbol }).Distinct().ToListAsync();

        return trades.Concat(transactions)
                     .OrderBy(t => t.Wallet)
                     .GroupBy(f => f.Wallet)
                     .Select(x => (x.Key, x.Select(f => f.Symbol).OrderBy(s => s).Distinct().ToArray())).ToList();
    }
}
