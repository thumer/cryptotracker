using CryptoTracker.Shared;
using System.Net.Http.Json;

namespace CryptoTracker.Client.RestClients;

public class CoinRatesRestClient : ICoinRatesApi
{
    private readonly HttpClient _http;

    public CoinRatesRestClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IList<CoinRateDTO>> GetCoinRatesAsync()
        => await _http.GetFromJsonAsync<IList<CoinRateDTO>>("api/CoinRates/GetCoinRates") ?? new List<CoinRateDTO>();

    public async Task SetManualCoinRateAsync(SetManualCoinRateRequest request)
        => await _http.PostAsJsonAsync("api/CoinRates/SetManualCoinRate", request);
}
