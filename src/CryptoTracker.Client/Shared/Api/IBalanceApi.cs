namespace CryptoTracker.Shared;

public interface IBalanceApi
{
    Task<IList<PlatformBalanceDTO>> GetBalancesAsync();
}
