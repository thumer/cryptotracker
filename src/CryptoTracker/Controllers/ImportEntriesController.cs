using CryptoTracker.Entities.Import;
using CryptoTracker.Shared;
using CryptoTracker.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CryptoTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportEntriesController : ControllerBase, IImportEntriesApi
{
    private readonly CryptoTrackerDbContext _dbContext;

    public ImportEntriesController(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("GetEntries")]
    public async Task<IActionResult> GetEntries(ImportDocumentType type)
    {
        var result = await LoadEntries(type);
        return Ok(result);
    }

    private async Task<object> LoadEntries(ImportDocumentType type)
    {
        return type switch
        {
            ImportDocumentType.BinanceDepositHistory =>
                (await _dbContext.BinanceDeposits.Include(e => e.Wallet).ToListAsync())
                    .Select(e => e.CloneToDTO<BinanceDepositDTO>()).ToList(),
            ImportDocumentType.BinanceWithdrawalHistory =>
                (await _dbContext.BinanceWithdrawals.Include(e => e.Wallet).ToListAsync())
                    .Select(e => e.CloneToDTO<BinanceWithdrawalDTO>()).ToList(),
            ImportDocumentType.BinanceTradingHistory =>
                (await _dbContext.BinanceTrades.Include(e => e.Wallet).ToListAsync())
                    .Select(e => e.CloneToDTO<BinanceTradeDTO>()).ToList(),
            ImportDocumentType.BitcoinDeTransactions =>
                (await _dbContext.BitcoinDeTransactions.Include(e => e.Wallet).ToListAsync())
                    .Select(e => e.CloneToDTO<BitcoinDeTransactionDTO>()).ToList(),
            ImportDocumentType.BitpandaTransaction =>
                (await _dbContext.BitpandaTransactions.Include(e => e.Wallet).ToListAsync())
                    .Select(e => e.CloneToDTO<BitpandaTransactionDTO>()).ToList(),
            ImportDocumentType.MetamaskTradingHistory =>
                (await _dbContext.MetamaskTrades.Include(e => e.Wallet).ToListAsync())
                    .Select(e => e.CloneToDTO<MetamaskTradeDTO>()).ToList(),
            ImportDocumentType.MetamaskTransactions =>
                (await _dbContext.MetamaskTransactions.Include(e => e.Wallet).ToListAsync())
                    .Select(e => e.CloneToDTO<MetamaskTransactionDTO>()).ToList(),
            ImportDocumentType.OkxDepositHistory =>
                (await _dbContext.OkxDeposits.Include(e => e.Wallet).ToListAsync())
                    .Select(e => e.CloneToDTO<OkxDepositDTO>()).ToList(),
            ImportDocumentType.OkxTradingHistory =>
                (await _dbContext.OkxTrades.Include(e => e.Wallet).ToListAsync())
                    .Select(e => e.CloneToDTO<OkxTradeDTO>()).ToList(),
            _ => new List<object>()
        };
    }

    async Task<IList<JsonElement>> IImportEntriesApi.GetEntriesAsync(ImportDocumentType type)
    {
        var result = await LoadEntries(type);
        var json = JsonSerializer.Serialize(result);
        return JsonSerializer.Deserialize<IList<JsonElement>>(json) ?? new List<JsonElement>();
    }
}
