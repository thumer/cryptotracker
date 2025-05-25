using CryptoTracker.Client.Shared;
using CryptoTracker.Common;
using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlowController : ControllerBase, IFlowApi
{
    private readonly FlowService _flowService;

    public FlowController(FlowService flowService)
    {
        _flowService = flowService;
    }

    [HttpGet("GetFlows")]
    public async Task<IActionResult> GetFlows(string walletName)
    {
        var flows = await _flowService.GetFlows(walletName);
        var bilanz = FlowService.CalculateBilanz(flows);

        var response = new FlowsResponse { Flows = flows.Select(f => f.CloneToDTO()).ToList(), Bilanz = bilanz };
        return Ok(response);
    }

    async Task<FlowsResponse?> IFlowApi.GetFlowsAsync(string walletName)
    {
        var flows = await _flowService.GetFlows(walletName);
        var bilanz = FlowService.CalculateBilanz(flows);
        return new FlowsResponse
        {
            Flows = flows.Select(f => f.CloneToDTO()).ToList(),
            Bilanz = bilanz
        };
    }
}
