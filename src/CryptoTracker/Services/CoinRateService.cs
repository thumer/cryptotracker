using CryptoTracker.Entities;
using CryptoTracker.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CryptoTracker.Services;

public class CoinRateService
{
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly IFinanceValueProvider _valueProvider;
    private readonly CoinMarketCapService _coinMarketCapService;
    private readonly ILogger<CoinRateService> _logger;

    public CoinRateService(CryptoTrackerDbContext dbContext,
        IFinanceValueProvider valueProvider,
        CoinMarketCapService coinMarketCapService,
        ILogger<CoinRateService> logger)
    {
        _dbContext = dbContext;
        _valueProvider = valueProvider;
        _coinMarketCapService = coinMarketCapService;
        _logger = logger;
    }

    public async Task<(decimal rate, bool isManual)> GetCurrentRateWithSourceAsync(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return (0m, false);

        var normalized = NormalizeSymbol(symbol);
        var rate = await _valueProvider.GetCurrentEuroValueAsync(normalized);
        if (rate > 0)
            return (rate, false);

        var manual = await GetLatestManualPriceAsync(normalized, null);
        return manual != null ? (manual.PriceEur, true) : (0m, false);
    }

    public async Task<(decimal? rate, bool isManual)> GetPreviousCloseRateWithSourceAsync(string symbol, DateTime dateUtc)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return (null, false);

        var normalized = NormalizeSymbol(symbol);
        var providerRate = await _coinMarketCapService.GetPreviousCloseAsync(normalized, dateUtc);
        if (providerRate.HasValue && providerRate.Value > 0)
            return (providerRate.Value, false);

        var manual = await GetLatestManualPriceAsync(normalized, dateUtc.Date);
        return manual != null ? (manual.PriceEur, true) : (null, false);
    }

    public Task<string?> GetSlugAsync(string symbol)
        => _coinMarketCapService.GetSlugAsync(symbol);

    public async Task<IList<CoinRateDTO>> GetCoinRatesAsync(IEnumerable<string> symbols)
    {
        var symbolList = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(NormalizeSymbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        var previousDate = DateTime.UtcNow.Date.AddDays(-1);
        var currentRates = await GetCurrentRatesAsync(symbolList);
        var previousRates = await GetPreviousCloseRatesAsync(symbolList, previousDate);
        var slugs = await GetSlugsAsync(symbolList);

        return symbolList.Select(symbol =>
        {
            var current = currentRates.TryGetValue(symbol, out var c) ? c : (rate: 0m, isManual: false);
            var previous = previousRates.TryGetValue(symbol, out var p) ? p : (rate: (decimal?)null, isManual: false);
            var slug = slugs.TryGetValue(symbol, out var s) ? s : null;

            return new CoinRateDTO(
                symbol,
                current.rate > 0 ? current.rate : null,
                previous.rate,
                previousDate,
                slug,
                current.rate > 0 && current.isManual,
                previous.rate.HasValue && previous.isManual);
        }).ToList();
    }

    public async Task<Dictionary<string, (decimal rate, bool isManual)>> GetCurrentRatesAsync(IEnumerable<string> symbols)
    {
        var symbolList = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(NormalizeSymbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var manualLookup = await GetLatestManualPricesAsync(symbolList, null);
        var result = new Dictionary<string, (decimal rate, bool isManual)>(StringComparer.OrdinalIgnoreCase);

        foreach (var symbol in symbolList)
        {
            var rate = await _valueProvider.GetCurrentEuroValueAsync(symbol);
            if (rate > 0)
            {
                result[symbol] = (rate, false);
                continue;
            }

            if (manualLookup.TryGetValue(symbol, out var manual))
            {
                result[symbol] = (manual.PriceEur, true);
            }
            else
            {
                result[symbol] = (0m, false);
            }
        }

        return result;
    }

    public async Task<Dictionary<string, (decimal? rate, bool isManual)>> GetPreviousCloseRatesAsync(IEnumerable<string> symbols, DateTime dateUtc)
    {
        var symbolList = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(NormalizeSymbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var manualLookup = await GetLatestManualPricesAsync(symbolList, dateUtc.Date);
        var result = new Dictionary<string, (decimal? rate, bool isManual)>(StringComparer.OrdinalIgnoreCase);

        foreach (var symbol in symbolList)
        {
            var rate = await _coinMarketCapService.GetPreviousCloseAsync(symbol, dateUtc);
            if (rate.HasValue && rate.Value > 0)
            {
                result[symbol] = (rate.Value, false);
                continue;
            }

            if (manualLookup.TryGetValue(symbol, out var manual))
            {
                result[symbol] = (manual.PriceEur, true);
            }
            else
            {
                result[symbol] = (null, false);
            }
        }

        return result;
    }

    public async Task<Dictionary<string, string?>> GetSlugsAsync(IEnumerable<string> symbols)
    {
        var symbolList = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(NormalizeSymbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return await _coinMarketCapService.GetSlugsAsync(symbolList);
    }

    public async Task SetManualPriceAsync(string symbol, DateTime date, decimal priceEur)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return;

        var normalized = NormalizeSymbol(symbol);
        var targetDate = date.Date;

        var existing = await _dbContext.ManualCoinPrices
            .FirstOrDefaultAsync(p => p.Symbol == normalized && p.Date == targetDate);

        if (existing == null)
        {
            _dbContext.ManualCoinPrices.Add(new ManualCoinPrice
            {
                Symbol = normalized,
                Date = targetDate,
                PriceEur = priceEur,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.PriceEur = priceEur;
            existing.CreatedAt = DateTimeOffset.UtcNow;
            _dbContext.ManualCoinPrices.Update(existing);
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task<ManualCoinPrice?> GetLatestManualPriceAsync(string symbol, DateTime? upToDate)
    {
        var query = _dbContext.ManualCoinPrices
            .AsNoTracking()
            .Where(p => p.Symbol == symbol);

        if (upToDate.HasValue)
        {
            var date = upToDate.Value.Date;
            query = query.Where(p => p.Date <= date);
        }

        return await query
            .OrderByDescending(p => p.Date)
            .FirstOrDefaultAsync();
    }

    private async Task<Dictionary<string, ManualCoinPrice>> GetLatestManualPricesAsync(IEnumerable<string> symbols, DateTime? upToDate)
    {
        var symbolList = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(NormalizeSymbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (symbolList.Count == 0)
            return new Dictionary<string, ManualCoinPrice>(StringComparer.OrdinalIgnoreCase);

        var query = _dbContext.ManualCoinPrices
            .AsNoTracking()
            .Where(p => symbolList.Contains(p.Symbol));

        if (upToDate.HasValue)
        {
            var date = upToDate.Value.Date;
            query = query.Where(p => p.Date <= date);
        }

        var prices = await query
            .OrderByDescending(p => p.Date)
            .ToListAsync();

        return prices
            .GroupBy(p => p.Symbol)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeSymbol(string symbol)
        => symbol.Trim().ToUpperInvariant();
}
