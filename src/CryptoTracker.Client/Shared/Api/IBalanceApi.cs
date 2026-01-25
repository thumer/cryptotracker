namespace CryptoTracker.Shared;

public interface IBalanceApi
{
    Task<IList<PlatformBalanceDTO>> GetBalancesAsync();
    Task<WalletBalanceDTO?> GetWalletBalanceAsync(string walletName);
}
