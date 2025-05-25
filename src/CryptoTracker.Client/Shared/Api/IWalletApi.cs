namespace CryptoTracker.Shared;

public interface IWalletApi
{
    Task<IList<WalletDTO>> GetWalletsAsync();
    Task<IList<WalletWithSymbolsDTO>> GetWalletsWithSymbolsAsync();
    Task<IList<WalletInfoDTO>> GetWalletInfosAsync();
    Task<WalletInfoDTO> SaveWalletAsync(WalletInfoDTO wallet);
    Task DeleteWalletAsync(int id);
}
