using CryptoTracker.Services;
using CryptoTracker.Shared;
using CryptoTracker.Entities;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WalletController : ControllerBase, IWalletApi
{
    private readonly WalletService _walletService;

    public WalletController(WalletService walletService)
    {
        _walletService = walletService;
    }

    [HttpGet("GetWallets")]
    public async Task<IActionResult> GetWallets()
    {
        var wallets = await _walletService.GetWallets();
        var walletDTOs = wallets.Select(w => new WalletDTO(w.Name)).ToList();

        return Ok(walletDTOs);
    }

    [HttpGet("GetWalletsWithSymbols")]
    public async Task<IActionResult> GetWalletsWithSymbols()
    {
        var wallets = await _walletService.GetWalletsWithSymbols();
        var walletDTOs = wallets.Select(w => new WalletWithSymbolsDTO(w.wallet.Name, w.symbols)).ToList();

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

    async Task<IList<WalletDTO>> IWalletApi.GetWalletsAsync()
        => (await _walletService.GetWallets()).Select(w => new WalletDTO(w.Name)).ToList();

    async Task<IList<WalletWithSymbolsDTO>> IWalletApi.GetWalletsWithSymbolsAsync()
    {
        var wallets = await _walletService.GetWalletsWithSymbols();
        return wallets.Select(w => new WalletWithSymbolsDTO(w.wallet.Name, w.symbols)).ToList();
    }

    async Task<IList<WalletInfoDTO>> IWalletApi.GetWalletInfosAsync()
        => (await _walletService.GetWallets()).Select(w => new WalletInfoDTO(w.Id, w.Name)).ToList();

    async Task<WalletInfoDTO> IWalletApi.SaveWalletAsync(WalletInfoDTO wallet)
    {
        var entity = await _walletService.SaveWallet(new Wallet { Id = wallet.Id, Name = wallet.Name });
        return new WalletInfoDTO(entity.Id, entity.Name);
    }

    Task IWalletApi.DeleteWalletAsync(int id)
    {
        return _walletService.DeleteWallet(id);
    }
}