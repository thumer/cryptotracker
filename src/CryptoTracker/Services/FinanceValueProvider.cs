using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoobsMuc.Coinmarketcap.Client;

namespace CryptoTracker.Services;

public class FinanceValueProvider : IFinanceValueProvider
{
    private readonly ICoinmarketcapClient _client;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FinanceValueProvider> _logger;

    public FinanceValueProvider(ICoinmarketcapClient client, IMemoryCache cache, ILogger<FinanceValueProvider> logger)
    {
        _client = client;
        _cache = cache;
        _logger = logger;
    }

    public async Task<decimal> GetCurrentEuroValueAsync(string symbol)
    {
        var cacheKey = $"eur-price-{symbol}";
        if (_cache.TryGetValue(cacheKey, out decimal cached))
        {
            return cached;
        }

        try
        {
            dynamic dynamicClient = _client;
            var response = await dynamicClient.GetCurrencyBySymbol(symbol);
            string json = JsonConvert.SerializeObject(response);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var price = doc.RootElement
                .GetProperty("data")
                .EnumerateObject().First().Value
                .GetProperty("quote")
                .GetProperty("EUR")
                .GetProperty("price")
                .GetDecimal();

            _cache.Set(cacheKey, price, TimeSpan.FromMinutes(15));
            return price;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve value for {Symbol}", symbol);
            return 0m;
        }
    }
}
