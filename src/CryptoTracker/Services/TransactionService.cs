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

    public async Task<IList<TransactionRowDTO>> GetTransactionsAsync(string? walletName, string? symbol, bool includeHidden)
    {
        var tradesQuery = _dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .Include(t => t.OppositeTrade)
            .AsQueryable();

        var transactionsQuery = _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Include(t => t.OppositeWallet)
            .AsQueryable();

        if (includeHidden)
        {
            tradesQuery = tradesQuery.IgnoreQueryFilters();
            transactionsQuery = transactionsQuery.IgnoreQueryFilters();
        }

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
            var fee = 0m;
            string? comment = null;
            var hasOpposite = false;
            var flowId = 0;
            var isHidden = false;

            if (f is CryptoTrade trade)
            {
                flowId = trade.Id;
                fee = trade.Fee;
                comment = trade.Comment;
                hasOpposite = trade.OppositeTradeId.HasValue;
                isHidden = trade.IsHidden;
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
            else if (f is CryptoTransaction transaction)
            {
                flowId = transaction.Id;
                fee = transaction.Fee;
                comment = transaction.Comment;
                hasOpposite = transaction.OppositeTransactionId.HasValue;
                isHidden = transaction.IsHidden;
            }

            return new TransactionRowDTO(f.FlowType,
                f.FlowDirection,
                f.DateTime,
                flowId,
                f.Symbol,
                f.FlowAmount,
                euroValue,
                rate,
                fee,
                f.SourceWallet,
                f.TargetWallet,
                slug,
                comment,
                hasOpposite,
                targetSymbol,
                targetAmount,
                targetSlug,
                Guid.NewGuid(),
                isHidden);
        }).ToList();
    }

    public async Task<FlowDetailsDTO?> GetTransactionDetailsAsync(FlowType flowType, int id, bool includeHidden)
    {
        return flowType switch
        {
            FlowType.Trade => await GetTradeDetailsAsync(id, includeHidden),
            FlowType.Transaction => await GetTransactionDetailsInternalAsync(id, includeHidden),
            _ => null
        };
    }

    public async Task<bool> SetHiddenAsync(FlowType flowType, int id, bool isHidden)
    {
        switch (flowType)
        {
            case FlowType.Trade:
            {
                var trade = await _dbContext.CryptoTrades.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
                if (trade == null)
                    return false;
                trade.IsHidden = isHidden;
                break;
            }
            case FlowType.Transaction:
            {
                var transaction = await _dbContext.CryptoTransactions.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
                if (transaction == null)
                    return false;
                transaction.IsHidden = isHidden;
                break;
            }
            default:
                return false;
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    private async Task<FlowDetailsDTO?> GetTradeDetailsAsync(int id, bool includeHidden)
    {
        var tradeQuery = _dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .Include(t => t.OppositeTrade)
            .ThenInclude(t => t!.Wallet)
            .AsQueryable();

        if (includeHidden)
        {
            tradeQuery = tradeQuery.IgnoreQueryFilters();
        }

        var trade = await tradeQuery.FirstOrDefaultAsync(t => t.Id == id);

        if (trade == null)
            return null;

        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            trade.Symbol,
            trade.OppositeSymbol
        };

        if (trade.OppositeTrade != null)
        {
            symbols.Add(trade.OppositeTrade.Symbol);
            symbols.Add(trade.OppositeTrade.OppositeSymbol);
        }

        var rates = await _coinRateService.GetCurrentRatesAsync(symbols);
        var slugs = await _coinRateService.GetSlugsAsync(symbols);

        var detail = BuildTradeDetails(trade, rates, slugs);
        var opposite = trade.OppositeTrade != null ? BuildTradeDetails(trade.OppositeTrade, rates, slugs) : null;

        return new FlowDetailsDTO(FlowType.Trade, detail, opposite, null, null);
    }

    private async Task<FlowDetailsDTO?> GetTransactionDetailsInternalAsync(int id, bool includeHidden)
    {
        var transactionQuery = _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Include(t => t.OppositeWallet)
            .Include(t => t.OppositeTransaction)
            .ThenInclude(t => t!.Wallet)
            .Include(t => t.OppositeTransaction)
            .ThenInclude(t => t!.OppositeWallet)
            .AsQueryable();

        if (includeHidden)
        {
            transactionQuery = transactionQuery.IgnoreQueryFilters();
        }

        var transaction = await transactionQuery.FirstOrDefaultAsync(t => t.Id == id);

        if (transaction == null)
            return null;

        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            transaction.Symbol
        };

        if (transaction.OppositeTransaction != null)
        {
            symbols.Add(transaction.OppositeTransaction.Symbol);
        }

        var rates = await _coinRateService.GetCurrentRatesAsync(symbols);
        var slugs = await _coinRateService.GetSlugsAsync(symbols);

        var detail = BuildTransactionDetails(transaction, rates, slugs);
        var opposite = transaction.OppositeTransaction != null ? BuildTransactionDetails(transaction.OppositeTransaction, rates, slugs) : null;

        return new FlowDetailsDTO(FlowType.Transaction, null, null, detail, opposite);
    }

    private static TradeDetailsDTO BuildTradeDetails(CryptoTrade trade,
        IReadOnlyDictionary<string, (decimal rate, bool isManual)> rates,
        IReadOnlyDictionary<string, string?> slugs)
    {
        var flow = (IFlow)trade;
        var rate = rates.TryGetValue(trade.Symbol, out var r) ? r.rate : 0m;
        var slug = slugs.TryGetValue(trade.Symbol, out var s) ? s : null;
        var euroValue = flow.FlowAmount * rate;

        string? targetSymbol;
        decimal? targetAmount;

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

        var targetSlug = !string.IsNullOrWhiteSpace(targetSymbol) && slugs.TryGetValue(targetSymbol, out var tSlug)
            ? tSlug
            : null;

        return new TradeDetailsDTO(trade.DateTime,
            flow.FlowDirection,
            trade.Symbol,
            flow.FlowAmount,
            euroValue,
            slug,
            targetSymbol,
            targetAmount,
            targetSlug,
            trade.Wallet.Name,
            trade.Fee,
            trade.Symbol,
            trade.ForeignFee,
            string.IsNullOrWhiteSpace(trade.ForeignFeeSymbol) ? null : trade.ForeignFeeSymbol,
            trade.Referenz,
            trade.Comment,
            trade.IsHidden);
    }

    private static TransactionDetailsDTO BuildTransactionDetails(CryptoTransaction transaction,
        IReadOnlyDictionary<string, (decimal rate, bool isManual)> rates,
        IReadOnlyDictionary<string, string?> slugs)
    {
        var flow = (IFlow)transaction;
        var rate = rates.TryGetValue(transaction.Symbol, out var r) ? r.rate : 0m;
        var slug = slugs.TryGetValue(transaction.Symbol, out var s) ? s : null;
        var euroValue = flow.FlowAmount * rate;

        return new TransactionDetailsDTO(transaction.DateTime,
            flow.FlowDirection,
            transaction.Symbol,
            flow.FlowAmount,
            euroValue,
            slug,
            flow.SourceWallet,
            flow.TargetWallet,
            transaction.Fee,
            transaction.Symbol,
            transaction.Comment,
            transaction.TransactionId,
            transaction.Address,
            transaction.Network,
            transaction.IsHidden);
    }
}
