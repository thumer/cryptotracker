using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Components.Forms;
using System.IO;
using System.Text.Json;

namespace CryptoTracker.Controllers
{
[ApiController]
[Route("api/[controller]")]
    public class DataImportController : ControllerBase, IDataImportApi
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

        private const long MAX_REQUEST_SIZE = 1024 * 1024 * 100;

        async Task IDataImportApi.ImportFileAsync(ImportDocumentType type, string walletName, IBrowserFile file)
        {
            using var memory = new MemoryStream();
            await file.OpenReadStream(MAX_REQUEST_SIZE).CopyToAsync(memory);
            await _dataImportService.Import(type, walletName, () => new MemoryStream(memory.ToArray()));
        }

        Task IDataImportApi.ProcessTransactionPairsAsync()
            => _dataImportService.ProcessTransactionPairs();
    }
}