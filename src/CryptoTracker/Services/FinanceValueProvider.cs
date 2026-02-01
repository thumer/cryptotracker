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
        if (string.IsNullOrWhiteSpace(symbol))
            return 0;

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
        catch (JsonSerializationException ex)
        {
            // Known issue with NoobsMuc.Coinmarketcap.Client: nullable int fields 
            // (like num_market_pairs) cause deserialization errors
            _logger.LogDebug(ex, "JSON deserialization error for {Symbol} - API response contains null values for non-nullable fields", symbol);
        }
        catch (InvalidCastException ex)
        {
            // Related to the JSON issue - null cannot be converted to value type
            _logger.LogDebug(ex, "Type conversion error for {Symbol}", symbol);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve value for {Symbol}", symbol);
        }

        // Cache even failed lookups to prevent repeated API calls for problematic symbols
        _cache.Set(symbol, price, TimeSpan.FromMinutes(price > 0 ? 15 : 5));
        await Task.CompletedTask;
        return price;
    }
}
