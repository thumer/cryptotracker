namespace CryptoTracker.Shared;

public interface IAssetFlowApi
{
    Task<IList<AssetFlowLineDTO>> GetAssetFlowsAsync(string walletName, string symbol);
}
