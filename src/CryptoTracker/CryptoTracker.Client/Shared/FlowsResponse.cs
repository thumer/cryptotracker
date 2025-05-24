using CryptoTracker.Shared;

namespace CryptoTracker.Client.Shared;

public class FlowsResponse
{
    public IList<FlowDTO> Flows { get; set; } = new List<FlowDTO>();
    public decimal Bilanz { get; set; }
}
