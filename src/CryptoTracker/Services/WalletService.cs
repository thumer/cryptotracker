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

    public async Task<IList<(Wallet wallet, string[] symbols)>> GetWalletsWithSymbols()
    {
        var trades = await _dbContext.CryptoTrades
            .Select(t => new { t.WalletId, t.Symbol })
            .Distinct()
            .ToListAsync();

        var transactions = await _dbContext.CryptoTransactions
            .Select(t => new { t.WalletId, t.Symbol })
            .Distinct()
            .ToListAsync();

        var symbolLookup = trades.Concat(transactions)
            .GroupBy(t => t.WalletId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Symbol).OrderBy(s => s).Distinct().ToArray());

        var wallets = await _dbContext.Wallets.OrderBy(w => w.Name).ToListAsync();
        return wallets.Select(w => (w, symbolLookup.TryGetValue(w.Id, out var syms) ? syms : Array.Empty<string>())).ToList();
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
