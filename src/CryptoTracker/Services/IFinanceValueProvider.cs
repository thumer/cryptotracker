namespace CryptoTracker.Services;

public interface IFinanceValueProvider
{
    Task<decimal> GetCurrentEuroValueAsync(string symbol);
}
