using CryptoTracker.Import.Objects;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportEntriesController : ControllerBase
{
    private readonly CryptoTrackerDbContext _dbContext;

    public ImportEntriesController(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("GetEntries")]
    public async Task<IActionResult> GetEntries(ImportDocumentType type)
    {
        object result = type switch
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

        return Ok(result);
    }
}
