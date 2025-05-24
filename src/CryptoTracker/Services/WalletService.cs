using Microsoft.EntityFrameworkCore;
using CryptoTracker.Entities;

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
        var trades = await _dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .Select(t => new { Wallet = t.Wallet.Name, t.Symbol })
            .Distinct()
            .ToListAsync();
        var transactions = await _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Select(t => new { Wallet = t.Wallet.Name, t.Symbol })
            .Distinct()
            .ToListAsync();

        return trades.Concat(transactions)
                     .OrderBy(t => t.Wallet)
                     .GroupBy(f => f.Wallet)
                    .Select(x => (x.Key, x.Select(f => f.Symbol).OrderBy(s => s).Distinct().ToArray())).ToList();
    }

    public async Task<IList<Wallet>> GetWallets()
        => await _dbContext.Wallets.OrderBy(w => w.Name).ToListAsync();

    public async Task<Wallet> SaveWallet(Wallet wallet)
    {
        if (wallet.Id == 0)
            _dbContext.Wallets.Add(wallet);
        else
            _dbContext.Wallets.Update(wallet);

        await _dbContext.SaveChangesAsync();
        return wallet;
    }

    public async Task DeleteWallet(int id)
    {
        var wallet = await _dbContext.Wallets.FindAsync(id);
        if (wallet != null)
        {
            _dbContext.Wallets.Remove(wallet);
            await _dbContext.SaveChangesAsync();
        }
    }
}
