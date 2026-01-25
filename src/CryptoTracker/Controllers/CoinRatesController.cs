using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CoinRatesController : ControllerBase, ICoinRatesApi
{
    private readonly BalanceService _balanceService;
    private readonly CoinRateService _coinRateService;

    public CoinRatesController(BalanceService balanceService, CoinRateService coinRateService)
    {
        _balanceService = balanceService;
        _coinRateService = coinRateService;
    }

    [HttpGet("GetCoinRates")]
    public async Task<IList<CoinRateDTO>> GetCoinRates()
    {
        var balances = await _balanceService.GetWalletBalancesAsync();
        var symbols = balances.SelectMany(b => b.Assets)
            .Select(a => a.Symbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return await _coinRateService.GetCoinRatesAsync(symbols);
    }

    [HttpPost("SetManualCoinRate")]
    public async Task<IActionResult> SetManualCoinRate([FromBody] SetManualCoinRateRequest request)
    {
        await _coinRateService.SetManualPriceAsync(request.Symbol, request.Date, request.PriceEur);
        return Ok();
    }

    Task<IList<CoinRateDTO>> ICoinRatesApi.GetCoinRatesAsync()
        => GetCoinRates();

    Task ICoinRatesApi.SetManualCoinRateAsync(SetManualCoinRateRequest request)
        => _coinRateService.SetManualPriceAsync(request.Symbol, request.Date, request.PriceEur);
}
