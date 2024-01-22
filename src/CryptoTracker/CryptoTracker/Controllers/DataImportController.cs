using Azure.Core;
using CryptoTracker.Services;
using Microsoft.AspNetCore.Mvc;
using System.Reflection.Metadata;

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
        public async Task<IActionResult> ImportFile(/*[FromForm(Name = "request")] string requestJson,*/ [FromForm] IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using TextReader reader = new StreamReader(stream);

            var data = await reader.ReadToEndAsync();
            _dataImportService.Import(data);

            return Ok("CryptoTrade erfolgreich importiert");
        }
    }
}
