using CryptoTracker.Client.Shared;
using CryptoTracker.Common;
using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlowController : ControllerBase
{
    private readonly FlowService _flowService;

    public FlowController(FlowService flowService)
    {
        _flowService = flowService;
    }

    [HttpGet("GetFlows")]
    public async Task<IActionResult> GetFlows(string walletName, string symbolName)
    {
        var flows = await _flowService.GetFlows(walletName, symbolName);
        var bilanz = FlowService.CalculateBilanz(flows);

        var response = new FlowsResponse { Flows = flows.Select(f => f.CloneToDTO()).ToList(), Bilanz = bilanz };
        return Ok(response);
    }
}
