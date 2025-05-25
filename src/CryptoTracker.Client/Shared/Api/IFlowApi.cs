using CryptoTracker.Client.Shared;

namespace CryptoTracker.Shared;

public interface IFlowApi
{
    Task<FlowsResponse?> GetFlowsAsync(string walletName);
}
