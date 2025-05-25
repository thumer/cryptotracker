using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components;

namespace CryptoTracker.Client.Pages;

public partial class Wallets
{
    private IList<WalletInfoDTO> WalletsList { get; set; } = new List<WalletInfoDTO>();
    private string EditName { get; set; } = string.Empty;
    private int EditId { get; set; }
    private string? ErrorMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private void NewWallet()
    {
        EditId = 0;
        EditName = string.Empty;
    }

    private void Edit(WalletInfoDTO wallet)
    {
        EditId = wallet.Id;
        EditName = wallet.Name;
    }

    private async Task Save()
    {
        var wallet = new WalletInfoDTO(EditId, EditName);
        try
        {
            await WalletApi.SaveWalletAsync(wallet);
            EditId = 0;
            EditName = string.Empty;
            await LoadData();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task Delete(int id)
    {
        try
        {
            await WalletApi.DeleteWalletAsync(id);
            await LoadData();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task LoadData()
    {
        WalletsList = await WalletApi.GetWalletInfosAsync();
    }
}
