using CryptoTracker.Entities;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportOverviewController : ControllerBase, IImportOverviewApi
{
    private readonly CryptoTrackerDbContext _dbContext;

    public ImportOverviewController(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
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

        var deposits = transactions
            .Where(t => t.TransactionType == TransactionType.Receive)
            .Select(MapTransaction)
            .OrderBy(t => t.DateTime)
            .ToList();

        var withdrawals = transactions
            .Where(t => t.TransactionType == TransactionType.Send)
            .Select(MapTransaction)
            .OrderBy(t => t.DateTime)
            .ToList();

        var tradeRows = trades
            .Select(MapTrade)
            .OrderBy(t => t.DateTime)
            .ToList();

        return new ImportOverviewDTO(deposits, withdrawals, tradeRows);
    }

    private static ImportTransactionRowDTO MapTransaction(CryptoTransaction transaction)
    {
        var type = transaction.TransactionType == TransactionType.Receive ? "Einzahlung" : "Auszahlung";
        var amount = transaction.TransactionType == TransactionType.Receive
            ? transaction.QuantityAfterFee
            : transaction.Quantity;

        return new ImportTransactionRowDTO(transaction.DateTime,
            type,
            transaction.Symbol,
            amount,
            transaction.Fee,
            transaction.Wallet.Name,
            transaction.Network,
            transaction.Address,
            transaction.Comment,
            transaction.TransactionId,
            Guid.NewGuid());
    }

    private static ImportTradeRowDTO MapTrade(CryptoTrade trade)
    {
        var side = trade.TradeType == TradeType.Buy ? "BUY" : "SELL";
        return new ImportTradeRowDTO(trade.DateTime,
            side,
            trade.Symbol,
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
