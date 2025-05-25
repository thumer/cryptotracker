using CryptoTracker.Shared;
using CryptoTracker.Client.Shared;
using Microsoft.AspNetCore.Components;

namespace CryptoTracker.Client.Pages
{
    public partial class Overview
    {
        private bool IsLoading { get; set; } = true;
        
        private string? ErrorMessage { get; set; }

        private IList<WalletDTO>? Wallets { get; set; }
        private IList<string>? Symbols { get; set; }

        private WalletDTO? SelectedWallet { get; set; }
        private string? SelectedWalletName { get; set; }
        private string? SelectedSymbol { get; set; }

        private IList<FlowDTO> Flows { get; set; } = new List<FlowDTO>();
        private decimal Balance { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            Wallets = await WalletApi.GetWalletsAsync();

            IsLoading = false;
        }

        private async Task OnSelectedWalletChanged(string walletName)
        {
            SelectedWalletName = walletName;
            SelectedWallet = Wallets?.FirstOrDefault(w => w.Name == walletName);
            Symbols = SelectedWallet?.Symbols.ToList();

            if (SelectedWallet != null && SelectedSymbol != null)
                await LoadData();
        }

        private async Task OnSelectedSymbolChanged(object value)
        {
            SelectedSymbol = value?.ToString();

            if (SelectedWallet != null && SelectedSymbol != null)
                await LoadData();
        }

        private async Task LoadData()
        {
            IsLoading = true;
            var response = await FlowApi.GetFlowsAsync(SelectedWallet?.Name ?? string.Empty, SelectedSymbol ?? string.Empty);
            if (response != null)
            {
                Flows = response.Flows ?? new List<FlowDTO>();
                Balance = response.Bilanz;
            }
            IsLoading = false;
        }
    }
}