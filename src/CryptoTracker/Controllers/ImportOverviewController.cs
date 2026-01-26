using CryptoTracker.Entities;
using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportOverviewController : ControllerBase, IImportOverviewApi
{
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly CoinRateService _coinRateService;

    public ImportOverviewController(CryptoTrackerDbContext dbContext, CoinRateService coinRateService)
    {
        _dbContext = dbContext;
        _coinRateService = coinRateService;
    }

    [HttpGet("GetOverview")]
    public async Task<IActionResult> GetOverview()
    {
        var result = await LoadOverviewAsync();
        return Ok(result);
    }

    async Task<ImportOverviewDTO> IImportOverviewApi.GetOverviewAsync()
        => await LoadOverviewAsync();

    private async Task<ImportOverviewDTO> LoadOverviewAsync()
    {
        var transactions = await _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .ToListAsync();

        var trades = await _dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .ToListAsync();

        var symbols = transactions.Select(t => t.Symbol)
            .Concat(trades.Select(t => t.Symbol))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var slugs = await _coinRateService.GetSlugsAsync(symbols);

        var deposits = transactions
            .Where(t => t.TransactionType == TransactionType.Receive)
            .Select(t => MapTransaction(t, slugs))
            .OrderBy(t => t.DateTime)
            .ToList();

        var withdrawals = transactions
            .Where(t => t.TransactionType == TransactionType.Send)
            .Select(t => MapTransaction(t, slugs))
            .OrderBy(t => t.DateTime)
            .ToList();

        var tradeRows = trades
            .Select(t => MapTrade(t, slugs))
            .OrderBy(t => t.DateTime)
            .ToList();

        return new ImportOverviewDTO(deposits, withdrawals, tradeRows);
    }

    private static ImportTransactionRowDTO MapTransaction(CryptoTransaction transaction, IReadOnlyDictionary<string, string?> slugs)
    {
        var type = transaction.TransactionType == TransactionType.Receive ? "Einzahlung" : "Auszahlung";
        var amount = transaction.TransactionType == TransactionType.Receive
            ? transaction.QuantityAfterFee
            : transaction.Quantity;
        var slug = slugs.TryGetValue(transaction.Symbol, out var value) ? value : null;

        return new ImportTransactionRowDTO(transaction.DateTime,
            type,
            transaction.Symbol,
            slug,
            amount,
            transaction.Fee,
            transaction.Wallet.Name,
            transaction.Network,
            transaction.Address,
            transaction.Comment,
            transaction.TransactionId,
            Guid.NewGuid());
    }

    private static ImportTradeRowDTO MapTrade(CryptoTrade trade, IReadOnlyDictionary<string, string?> slugs)
    {
        var side = trade.TradeType == TradeType.Buy ? "BUY" : "SELL";
        var slug = slugs.TryGetValue(trade.Symbol, out var value) ? value : null;
        return new ImportTradeRowDTO(trade.DateTime,
            side,
            trade.Symbol,
            slug,
            trade.OppositeSymbol,
            trade.Price,
            trade.Quantity,
            trade.Fee,
            trade.Wallet.Name,
            trade.Comment,
            trade.Referenz,
            Guid.NewGuid());
    }
}
