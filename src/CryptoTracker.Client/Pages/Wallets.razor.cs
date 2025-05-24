using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

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
        var response = await HttpClient.PostAsJsonAsync("api/Wallet/SaveWallet", wallet);
        if (response.IsSuccessStatusCode)
        {
            EditId = 0;
            EditName = string.Empty;
            await LoadData();
        }
        else
        {
            ErrorMessage = await response.Content.ReadAsStringAsync();
        }
    }

    private async Task Delete(int id)
    {
        var response = await HttpClient.DeleteAsync($"api/Wallet/{id}");
        if (response.IsSuccessStatusCode)
        {
            await LoadData();
        }
        else
        {
            ErrorMessage = await response.Content.ReadAsStringAsync();
        }
    }

    private async Task LoadData()
    {
        WalletsList = await HttpClient.GetFromJsonAsync<IList<WalletInfoDTO>>("api/Wallet/GetWalletInfos") ?? new List<WalletInfoDTO>();
    }
}
