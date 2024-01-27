using CryptoTracker.Import;
using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CryptoTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataImportController : ControllerBase
    {
        private readonly DataImportService _dataImportService;
        private readonly CryptoTrackerDbContext _dbContext;

        public DataImportController(DataImportService dataImportService, CryptoTrackerDbContext dbContext)
        {
            _dataImportService = dataImportService;
            _dbContext = dbContext;
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> ImportFile([FromForm(Name = "request")] string requestJson, [FromForm] IFormFile file)
        {
            var request = JsonSerializer.Deserialize<ImportFileRequest>(requestJson, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            var importer = GetImporter(request.Type);
            importer.Import(file.OpenReadStream);

            return Ok("CryptoTrade erfolgreich importiert");
        }

        private IImporter GetImporter(ImportDocumentType type)
            => type switch
            {
                ImportDocumentType.BinanceDepositHistory => new BinanceDepositImporter(_dbContext),
                ImportDocumentType.BinanceTradingHistory => new BinanceTradeImporter(_dbContext),
                ImportDocumentType.BinanceWithdrawalHistory => new BinanceWithdrawalImporter(_dbContext),
                ImportDocumentType.BitcoinDeTransactions => new BitcoinDeTransactionImporter(_dbContext),
                ImportDocumentType.BitpandaTransaction => new BitpandaTransactionImporter(_dbContext),
                ImportDocumentType.MetamaskTradingHistory => new MetamaskTradeImporter(_dbContext),
                ImportDocumentType.MetamaskTransactions => new MetamaskTransactionImporter(_dbContext),
                ImportDocumentType.OkxDepositHistory => new OkxDepositImporter(_dbContext),
                ImportDocumentType.OkxTradingHistory => new OkxTradeImporter(_dbContext)
            };
    }
}