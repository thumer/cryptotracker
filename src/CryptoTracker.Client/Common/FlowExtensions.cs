using CryptoTracker.Shared;

namespace CryptoTracker.Common;

public static class FlowExtensions
{
    public static FlowDTO CloneToDTO(this IFlow flow)
        => new FlowDTO(
            flow.FlowType,
            flow.DateTime,
            flow.Symbol,
            flow.SourceWallet,
            flow.TargetWallet,
            flow.FlowDirection,
            flow.FlowAmount);
}