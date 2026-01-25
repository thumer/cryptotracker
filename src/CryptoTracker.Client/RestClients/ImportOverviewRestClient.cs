using System.Net.Http.Json;
using CryptoTracker.Shared;

namespace CryptoTracker.Client.RestClients;

public class ImportOverviewRestClient : IImportOverviewApi
{
    private readonly HttpClient _http;

    public ImportOverviewRestClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ImportOverviewDTO> GetOverviewAsync()
        => await _http.GetFromJsonAsync<ImportOverviewDTO>("api/ImportOverview/GetOverview")
           ?? new ImportOverviewDTO(new List<ImportTransactionRowDTO>(), new List<ImportTransactionRowDTO>(), new List<ImportTradeRowDTO>());
}
