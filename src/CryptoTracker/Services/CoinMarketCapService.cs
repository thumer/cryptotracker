using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CryptoTracker.Services;

public class CoinMarketCapService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CoinMarketCapService> _logger;
    private readonly string? _apiKey;

    public CoinMarketCapService(IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<CoinMarketCapService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
        _apiKey = configuration["COINMARKETCAP_API_KEY"];
    }

    public async Task<string?> GetSlugAsync(string symbol)
    {
        var slugs = await GetSlugsAsync(new[] { symbol });
        var normalized = symbol.Trim().ToUpperInvariant();
        return slugs.TryGetValue(normalized, out var slug) ? slug : null;
    }

    public async Task<Dictionary<string, string?>> GetSlugsAsync(IEnumerable<string> symbols)
    {
        var symbolList = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var symbol in symbolList)
        {
            var cacheKey = $"cmc:slug:{symbol}";
            if (_cache.TryGetValue(cacheKey, out string? cached))
            {
                result[symbol] = cached;
            }
        }

        var missing = symbolList.Where(s => !result.ContainsKey(s)).ToList();
        if (missing.Count == 0)
            return result;

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            foreach (var symbol in missing)
            {
                result[symbol] = null;
            }
            return result;
        }

        const int chunkSize = 100;
        for (var i = 0; i < missing.Count; i += chunkSize)
        {
            var chunk = missing.Skip(i).Take(chunkSize).ToList();
            var lookup = await FetchSlugMapAsync(chunk);
            foreach (var symbol in chunk)
            {
                if (lookup.TryGetValue(symbol, out var entry))
                {
                    result[symbol] = entry.slug;
                    if (entry.success)
                    {
                        var ttl = string.IsNullOrWhiteSpace(entry.slug) ? TimeSpan.FromHours(1) : TimeSpan.FromDays(7);
                        _cache.Set<string?>($"cmc:slug:{symbol}", entry.slug, ttl);
                    }
                }
                else
                {
                    result[symbol] = null;
                }
            }
        }

        return result;
    }

    public async Task<decimal?> GetPreviousCloseAsync(string symbol, DateTime dateUtc)
    {
        if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(_apiKey))
            return null;

        var normalized = symbol.Trim().ToUpperInvariant();
        var date = dateUtc.Date;
        var cacheKey = $"cmc:close:{normalized}:{date:yyyyMMdd}";
        if (_cache.TryGetValue(cacheKey, out decimal? cached))
            return cached;

        try
        {
            var client = CreateClient();
            var timeStart = date.ToString("yyyy-MM-dd");
            var timeEnd = date.AddDays(1).ToString("yyyy-MM-dd");
            var url = $"v2/cryptocurrency/ohlcv/historical?symbol={Uri.EscapeDataString(normalized)}&convert=EUR&time_start={timeStart}&time_end={timeEnd}&interval=1d";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CoinMarketCap historical lookup failed for {Symbol} with status {Status}", normalized, response.StatusCode);
                _cache.Set<decimal?>(cacheKey, null, TimeSpan.FromHours(6));
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            if (!doc.RootElement.TryGetProperty("data", out var data))
            {
                _cache.Set<decimal?>(cacheKey, null, TimeSpan.FromHours(6));
                return null;
            }

            JsonElement quotes;
            if (data.TryGetProperty("quotes", out quotes) && quotes.ValueKind == JsonValueKind.Array && quotes.GetArrayLength() > 0)
            {
                var last = quotes[quotes.GetArrayLength() - 1];
                var close = TryGetCloseFromQuote(last);
                _cache.Set<decimal?>(cacheKey, close, TimeSpan.FromHours(12));
                return close;
            }

            if (data.TryGetProperty("symbol", out _))
            {
                _cache.Set<decimal?>(cacheKey, null, TimeSpan.FromHours(6));
                return null;
            }

            _cache.Set<decimal?>(cacheKey, null, TimeSpan.FromHours(6));
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CoinMarketCap historical lookup failed for {Symbol}", symbol);
            _cache.Set<decimal?>(cacheKey, null, TimeSpan.FromHours(6));
            return null;
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("CoinMarketCap");
        if (!client.DefaultRequestHeaders.Contains("X-CMC_PRO_API_KEY") && !string.IsNullOrWhiteSpace(_apiKey))
        {
            client.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", _apiKey);
        }
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private async Task<Dictionary<string, (string? slug, bool success)>> FetchSlugMapAsync(IList<string> symbols)
    {
        var result = new Dictionary<string, (string? slug, bool success)>(StringComparer.OrdinalIgnoreCase);

        foreach (var symbol in symbols)
        {
            var (slug, success) = await FetchSlugForSymbolAsync(symbol);
            result[symbol] = (slug, success);
        }

        return result;
    }

    private async Task<(string? slug, bool success)> FetchSlugForSymbolAsync(string symbol)
    {
        try
        {
            var client = CreateClient();
            var symbolParam = Uri.EscapeDataString(symbol);
            var url = $"v1/cryptocurrency/map?symbol={symbolParam}&aux=is_active";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CoinMarketCap map lookup failed for symbol {Symbol} with status {Status}", symbol, response.StatusCode);
                return (null, false);
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                return (null, false);
            }

            var candidates = new List<MapEntry>();
            foreach (var item in data.EnumerateArray())
            {
                if (!item.TryGetProperty("symbol", out var symbolProp))
                    continue;

                var itemSymbol = symbolProp.GetString();
                if (string.IsNullOrWhiteSpace(itemSymbol))
                    continue;

                var slug = item.TryGetProperty("slug", out var slugProp) ? slugProp.GetString() : null;
                var rank = TryGetInt(item, "rank") ?? TryGetInt(item, "cmc_rank");
                var isActive = TryGetInt(item, "is_active");

                if (itemSymbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(new MapEntry(itemSymbol, slug, rank, isActive));
                }
            }

            if (candidates.Count == 0)
                return (null, true);

            var chosen = candidates
                .OrderByDescending(c => c.IsActive == 1)
                .ThenBy(c => c.Rank ?? int.MaxValue)
                .First();

            return (chosen.Slug, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CoinMarketCap map lookup failed for symbol {Symbol}", symbol);
            return (null, false);
        }
    }

    private static int? TryGetInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var prop))
            return null;
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value))
            return value;
        return null;
    }

    private sealed record MapEntry(string Symbol, string? Slug, int? Rank, int? IsActive);

    private static decimal? TryGetCloseFromQuote(JsonElement quoteElement)
    {
        if (!quoteElement.TryGetProperty("quote", out var quote))
            return null;
        if (!quote.TryGetProperty("EUR", out var eur))
            return null;
        if (!eur.TryGetProperty("close", out var close))
            return null;
        if (close.ValueKind == JsonValueKind.Number && close.TryGetDecimal(out var value))
            return value;
        return null;
    }
}
