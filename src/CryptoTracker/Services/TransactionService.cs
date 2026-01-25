using CryptoTracker.Entities;
using CryptoTracker.Shared;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Services;

public class TransactionService
{
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly CoinRateService _coinRateService;

    public TransactionService(CryptoTrackerDbContext dbContext, CoinRateService coinRateService)
    {
        _dbContext = dbContext;
        _coinRateService = coinRateService;
    }

    public async Task<IList<TransactionRowDTO>> GetTransactionsAsync(string? walletName, string? symbol)
    {
        var tradesQuery = _dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .Include(t => t.OppositeTrade)
            .AsQueryable();

        var transactionsQuery = _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Include(t => t.OppositeWallet)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(walletName))
        {
            tradesQuery = tradesQuery.Where(t => t.Wallet.Name == walletName);
            transactionsQuery = transactionsQuery.Where(t => t.Wallet.Name == walletName);
        }

        if (!string.IsNullOrWhiteSpace(symbol))
        {
            var normalized = symbol.Trim().ToUpperInvariant();
            tradesQuery = tradesQuery.Where(t => t.Symbol == normalized);
            transactionsQuery = transactionsQuery.Where(t => t.Symbol == normalized);
        }

        var trades = await tradesQuery.ToListAsync();
        var transactions = await transactionsQuery.ToListAsync();

        var flows = trades.Cast<IFlow>()
            .Concat(transactions)
            .OrderBy(f => f.DateTime)
            .ToList();

        var rateSymbols = flows.Select(f => f.Symbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var slugSymbols = flows.Select(f => f.Symbol)
            .Concat(trades.Select(t => t.OppositeSymbol))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rateLookup = await _coinRateService.GetCurrentRatesAsync(rateSymbols);
        var slugLookup = await _coinRateService.GetSlugsAsync(slugSymbols);

        return flows.Select(f =>
        {
            var rate = rateLookup.TryGetValue(f.Symbol, out var r) ? r.rate : 0m;
            var slug = slugLookup.TryGetValue(f.Symbol, out var s) ? s : null;
            var euroValue = f.FlowAmount * rate;
            string? targetSymbol = null;
            decimal? targetAmount = null;
            string? targetSlug = null;

            if (f is CryptoTrade trade)
            {
                if (trade.TradeType == TradeType.Sell)
                {
                    targetSymbol = trade.OppositeSymbol;
                    targetAmount = trade.OppositeTrade?.QuantityAfterFee ?? trade.Price * trade.Quantity;
                }
                else
                {
                    targetSymbol = trade.Symbol;
                    targetAmount = trade.QuantityAfterFee;
                }

                if (!string.IsNullOrWhiteSpace(targetSymbol) && slugLookup.TryGetValue(targetSymbol, out var tSlug))
                {
                    targetSlug = tSlug;
                }
            }

            return new TransactionRowDTO(f.FlowType,
                f.FlowDirection,
                f.DateTime,
                f.Symbol,
                f.FlowAmount,
                euroValue,
                rate,
                f.SourceWallet,
                f.TargetWallet,
                slug,
                targetSymbol,
                targetAmount,
                targetSlug,
                Guid.NewGuid());
        }).ToList();
    }
}
