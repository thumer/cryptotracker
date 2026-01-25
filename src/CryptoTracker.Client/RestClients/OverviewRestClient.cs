using CryptoTracker.Shared;
using System.Net.Http.Json;

namespace CryptoTracker.Client.RestClients;

public class OverviewRestClient : IOverviewApi
{
    private readonly HttpClient _http;

    public OverviewRestClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<OverviewSummaryDTO> GetOverviewAsync()
        => await _http.GetFromJsonAsync<OverviewSummaryDTO>("api/Overview/GetOverview") ?? new OverviewSummaryDTO(0, 0, new List<CoinSummaryDTO>(), new List<WalletSummaryDTO>());
}
