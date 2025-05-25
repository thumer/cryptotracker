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
        if (_cache.TryGetValue(symbol, out decimal cached))
        {
            return cached;
        }

        decimal price = 0;
        try
        {
            var response = _client.GetCurrencyBySymbol(symbol, "EUR");
            price = response.Price;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve value for {Symbol}", symbol);
        }
        _cache.Set(symbol, price, TimeSpan.FromMinutes(15));
        return price;
    }
}
