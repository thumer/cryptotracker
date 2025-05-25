using CryptoTracker.Shared;
using System.Net.Http.Json;

namespace CryptoTracker.Client.RestClients;

public class BalanceRestClient : IBalanceApi
{
    private readonly HttpClient _http;

    public BalanceRestClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IList<PlatformBalanceDTO>> GetBalancesAsync()
        => await _http.GetFromJsonAsync<IList<PlatformBalanceDTO>>("api/Balance/GetBalances") ?? new List<PlatformBalanceDTO>();
}
