using CryptoTracker.Shared;
using System.Net.Http.Json;

namespace CryptoTracker.Client.RestClients;

public class AssetFlowRestClient : IAssetFlowApi
{
    private readonly HttpClient _http;

    public AssetFlowRestClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IList<AssetFlowLineDTO>> GetAssetFlowsAsync(string walletName, string symbol)
        => await _http.GetFromJsonAsync<IList<AssetFlowLineDTO>>($"api/AssetFlow/GetAssetFlows?walletName={walletName}&symbol={symbol}")
           ?? new List<AssetFlowLineDTO>();
}
