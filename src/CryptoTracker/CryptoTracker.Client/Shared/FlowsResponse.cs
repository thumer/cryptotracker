using CryptoTracker.Shared;

namespace CryptoTracker.Client.Shared;

public class FlowsResponse
{
    public IList<FlowDTO> Flows { get; set; }
    public decimal Bilanz { get; set; }
}
