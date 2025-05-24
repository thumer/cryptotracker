using Finance.Net;

namespace CryptoTracker.Services;

public class FinanceValueProvider : IFinanceValueProvider
{
    private readonly YahooFinance _client = new YahooFinance();

    public async Task<decimal> GetCurrentEuroValueAsync(string symbol)
    {
        // Finance.Net expects trading pair symbols like BTC-EUR
        var quote = await _client.GetQuoteAsync($"{symbol}-EUR");
        return quote.RegularMarketPrice;
    }
}
