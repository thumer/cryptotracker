﻿using CryptoTracker.Entities;
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

    public async Task<IList<IFlow>> GetFlows(string walletName)
    {
        var trades = await _dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .Where(t => t.Wallet.Name == walletName)
            .ToListAsync();
        var transactions = await _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Include(t => t.OppositeWallet)
            .Where(t => t.Wallet.Name == walletName)
            .ToListAsync();

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
