using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OverviewController : ControllerBase, IOverviewApi
{
    private readonly OverviewService _overviewService;

    public OverviewController(OverviewService overviewService)
    {
        _overviewService = overviewService;
    }

    [HttpGet("GetOverview")]
    public Task<OverviewSummaryDTO> GetOverview()
        => _overviewService.GetOverviewAsync();

    Task<OverviewSummaryDTO> IOverviewApi.GetOverviewAsync()
        => _overviewService.GetOverviewAsync();
}
