using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssetFlowController : ControllerBase, IAssetFlowApi
{
    private readonly AssetFlowService _service;

    public AssetFlowController(AssetFlowService service)
    {
        _service = service;
    }

    [HttpGet("GetAssetFlows")]
    public async Task<IList<AssetFlowLineDTO>> GetAssetFlows(string walletName, string symbol)
        => await _service.GetAssetFlows(walletName, symbol);

    Task<IList<AssetFlowLineDTO>> IAssetFlowApi.GetAssetFlowsAsync(string walletName, string symbol)
        => _service.GetAssetFlows(walletName, symbol);
}
