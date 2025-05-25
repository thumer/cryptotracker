using System.Text.Json;
using CryptoTracker.Shared;
using System.Net.Http.Json;

namespace CryptoTracker.Client.RestClients;

public class ImportEntriesRestClient : IImportEntriesApi
{
    private readonly HttpClient _http;

    public ImportEntriesRestClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IList<JsonElement>> GetEntriesAsync(ImportDocumentType type)
        => await _http.GetFromJsonAsync<IList<JsonElement>>($"api/ImportEntries/GetEntries?type={type}") ?? new List<JsonElement>();
}
