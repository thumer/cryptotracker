using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace CryptoTracker.Client.RestClients;

public class DataImportRestClient : IDataImportApi
{
    private readonly HttpClient _http;
    private const long MAX_REQUEST_SIZE = 1024 * 1024 * 100;

    public DataImportRestClient(HttpClient http)
    {
        _http = http;
    }

    public async Task ImportFileAsync(ImportDocumentType type, string walletName, IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(file.OpenReadStream(MAX_REQUEST_SIZE));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(fileContent, "file", file.Name);
        content.Add(JsonContent.Create(new ImportFileRequest { Type = type, WalletName = walletName }), "request");
        var response = await _http.PostAsync("api/DataImport/ImportFile", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task ProcessTransactionPairsAsync()
    {
        using var content = new MultipartFormDataContent();
        var response = await _http.PostAsync("api/DataImport/ProcessTransactionPairs", content);
        response.EnsureSuccessStatusCode();
    }
}
