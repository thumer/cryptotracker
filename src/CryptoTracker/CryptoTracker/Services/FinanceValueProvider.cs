using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace CryptoTracker.Services;

public class FinanceValueProvider : IFinanceValueProvider
{
    private readonly HttpClient _httpClient;

    public FinanceValueProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<decimal> GetCurrentEuroValueAsync(string symbol)
    {
        try
        {
            var url = $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={symbol}-EUR";
            var json = await _httpClient.GetFromJsonAsync<JsonElement>(url);

            return json.GetProperty("quoteResponse")
                .GetProperty("result")[0]
                .GetProperty("regularMarketPrice")
                .GetDecimal();
        }
        catch
        {
            return 0m;
        }
    }
}
