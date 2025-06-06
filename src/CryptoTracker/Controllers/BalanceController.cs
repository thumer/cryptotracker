using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BalanceController : ControllerBase, IBalanceApi
{
    private readonly BalanceService _balanceService;

    public BalanceController(BalanceService balanceService)
    {
        _balanceService = balanceService;
    }

    [HttpGet("GetBalances")]
    public async Task<IList<PlatformBalanceDTO>> GetBalances()
    {
        return await _balanceService.GetBalances();
    }

    Task<IList<PlatformBalanceDTO>> IBalanceApi.GetBalancesAsync()
        => _balanceService.GetBalances();
}
