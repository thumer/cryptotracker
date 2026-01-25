namespace CryptoTracker.Shared;

public interface ICoinRatesApi
{
    Task<IList<CoinRateDTO>> GetCoinRatesAsync();
    Task SetManualCoinRateAsync(SetManualCoinRateRequest request);
}
