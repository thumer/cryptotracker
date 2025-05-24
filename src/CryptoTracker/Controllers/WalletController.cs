using CryptoTracker.Services;
using CryptoTracker.Shared;
using CryptoTracker.Entities;
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

    [HttpGet("GetWalletInfos")]
    public async Task<IActionResult> GetWalletInfos()
        => Ok(await _walletService.GetWallets());

    [HttpPost("SaveWallet")]
    public async Task<IActionResult> SaveWallet([FromBody] Wallet wallet)
        => Ok(await _walletService.SaveWallet(wallet));

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteWallet(int id)
    {
        await _walletService.DeleteWallet(id);
        return Ok();
    }
}