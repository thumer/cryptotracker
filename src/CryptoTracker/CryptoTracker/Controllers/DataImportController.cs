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

        public DataImportController(DataImportService dataImportService)
        {
            _dataImportService = dataImportService;
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> ImportFile([FromForm(Name = "request")] string requestJson, [FromForm] IFormFile file)
        {
            var request = JsonSerializer.Deserialize<ImportFileRequest>(requestJson, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            await _dataImportService.Import(request.Type, request.WalletName, file.OpenReadStream);
            return Ok("CryptoTrade erfolgreich importiert");
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> ProcessTransactionPairs()
        {
            await _dataImportService.ProcessTransactionPairs();
            return Ok("Transaktionen wurden erfolgreich zusammengeführt");
        }
    }
}