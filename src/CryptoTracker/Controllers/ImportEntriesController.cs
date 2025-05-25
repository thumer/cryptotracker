using CryptoTracker.Entities.Import;
using CryptoTracker.Shared;
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
            ImportDocumentType.BinanceDepositHistory => await _dbContext.BinanceDeposits.ToListAsync(),
            ImportDocumentType.BinanceWithdrawalHistory => await _dbContext.BinanceWithdrawals.ToListAsync(),
            ImportDocumentType.BinanceTradingHistory => await _dbContext.BinanceTrades.ToListAsync(),
            ImportDocumentType.BitcoinDeTransactions => await _dbContext.BitcoinDeTransactions.ToListAsync(),
            ImportDocumentType.BitpandaTransaction => await _dbContext.BitpandaTransactions.ToListAsync(),
            ImportDocumentType.MetamaskTradingHistory => await _dbContext.MetamaskTrades.ToListAsync(),
            ImportDocumentType.MetamaskTransactions => await _dbContext.MetamaskTransactions.ToListAsync(),
            ImportDocumentType.OkxDepositHistory => await _dbContext.OkxDeposits.ToListAsync(),
            ImportDocumentType.OkxTradingHistory => await _dbContext.OkxTrades.ToListAsync(),
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
