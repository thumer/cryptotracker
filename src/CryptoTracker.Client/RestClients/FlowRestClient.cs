using CryptoTracker.Client.Shared;
using CryptoTracker.Shared;
using System.Net.Http.Json;

namespace CryptoTracker.Client.RestClients;

public class FlowRestClient : IFlowApi
{
    private readonly HttpClient _http;

    public FlowRestClient(HttpClient http)
    {
        _http = http;
    }

    public Task<FlowsResponse?> GetFlowsAsync(string walletName, string symbolName)
        => _http.GetFromJsonAsync<FlowsResponse>($"api/Flow/GetFlows?walletName={walletName}&symbolName={symbolName}");
}
