using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WalletController : ControllerBase
{
    private readonly WalletService _walletService;

    public WalletController(WalletService walletService)
    {
        _walletService = walletService;
    }

    [HttpGet("GetWallets")]
    public async Task<IActionResult> GetWallets()
    {
        var wallets = await _walletService.GetWalletAndSymbols();
        var walletDTOs = wallets.Select(w => new WalletDTO(w.wallet, w.symbols)).ToList();

        return Ok(walletDTOs);
    }
}