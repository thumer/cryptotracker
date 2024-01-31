using CryptoTracker.Entities;
using CryptoTracker.Shared;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Services;

public class FlowService
{
    private readonly CryptoTrackerDbContext _dbContext;

    public FlowService(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IList<IFlow>> GetFlows(string walletName, string symbolName)
    {
        var trades = await _dbContext.CryptoTrades.Where(t => t.Wallet == walletName && t.Symbol == symbolName).ToListAsync();
        var transactions = await _dbContext.CryptoTransactions.Where(t => t.Wallet == walletName && t.Symbol == symbolName).ToListAsync();

        var flowList = trades.Cast<IFlow>().Concat(transactions).OrderBy(f => f.DateTime).ToList();
        return flowList;
    }

    public static decimal CalculateBilanz(IList<IFlow> flows)
    {
        decimal bilanz = 0;
        foreach (var f in flows)
        {
            if (f.FlowDirection == FlowDirection.Inflow)
                bilanz += f.FlowAmount;
            else
                bilanz -= f.FlowAmount;
        }
        return bilanz;
    }
}
