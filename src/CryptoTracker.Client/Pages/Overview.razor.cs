using CryptoTracker.Shared;
using CryptoTracker.Client.Shared;
using Microsoft.AspNetCore.Components;

namespace CryptoTracker.Client.Pages
{
    public partial class Overview
    {
        private bool IsLoading { get; set; } = true;
        
        private string? ErrorMessage { get; set; }

        private IList<WalletWithSymbolsDTO>? Wallets { get; set; }

        private WalletWithSymbolsDTO? SelectedWallet { get; set; }
        private string? SelectedWalletName { get; set; }

        private IList<FlowDTO> Flows { get; set; } = new List<FlowDTO>();
        private decimal Balance { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            Wallets = await WalletApi.GetWalletsWithSymbolsAsync();

            IsLoading = false;
        }

        private async Task OnSelectedWalletChanged(string walletName)
        {
            SelectedWalletName = walletName;
            SelectedWallet = Wallets?.FirstOrDefault(w => w.Name == walletName);

            if (SelectedWallet != null)
                await LoadData();
        }

        private async Task LoadData()
        {
            IsLoading = true;
            var response = await FlowApi.GetFlowsAsync(SelectedWallet?.Name ?? string.Empty);
            if (response != null)
            {
                Flows = response.Flows ?? new List<FlowDTO>();
                Balance = response.Bilanz;
            }
            IsLoading = false;
        }
    }
}