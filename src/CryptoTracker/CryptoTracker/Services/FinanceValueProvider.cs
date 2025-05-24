using Finance.Net.Interfaces;
using Finance.Net.Models.Yahoo;
using Finance.Net.Exceptions;

namespace CryptoTracker.Services;

public class FinanceValueProvider : IFinanceValueProvider
{
    private readonly IYahooFinanceService _financeService;

    public FinanceValueProvider(IYahooFinanceService financeService)
    {
        _financeService = financeService;
    }

    public async Task<decimal> GetCurrentEuroValueAsync(string symbol)
    {
        try
        {
            Quote quote = await _financeService.GetQuoteAsync($"{symbol}-EUR");
            return quote.RegularMarketPrice;
        }
        catch (FinanceException)
        {
            return 0m;
        }
    }
}
