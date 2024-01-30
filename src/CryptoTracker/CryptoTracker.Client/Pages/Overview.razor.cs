using CryptoTracker.Shared;
using System.Net.Http.Json;
using CryptoTracker.Client.Shared;

namespace CryptoTracker.Client.Pages
{
    public partial class Overview
    {
        private bool IsLoading { get; set; } = true;
        
        private string? ErrorMessage { get; set; }

        private IList<WalletDTO>? Wallets { get; set; }
        private IList<string>? Symbols { get; set; }

        private WalletDTO? SelectedWallet { get; set; }
        private string? SelectedSymbol { get; set; }

        private IList<FlowDTO> Flows { get; set; }
        private decimal Balance { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            Wallets = await HttpClient.GetFromJsonAsync<IList<WalletDTO>>("api/Wallet/GetWallets");

            IsLoading = false;
        }

        private async Task OnSelectedWalletChanged(WalletDTO wallet)
        {
            SelectedWallet = wallet;
            Symbols = wallet.Symbols.ToList();

            if (SelectedWallet != null && SelectedSymbol != null)
                await LoadData();
        }

        private async Task OnSelectedSymbolChanged(string symbol)
        {
            SelectedSymbol = symbol;

            if (SelectedWallet != null && SelectedSymbol != null)
                await LoadData();
        }

        private async Task LoadData()
        {
            IsLoading = true;
            var response = await HttpClient.GetFromJsonAsync<FlowsResponse>($"api/Flow/GetFlows?walletName={SelectedWallet?.Name}&symbolName={SelectedSymbol}");
            Flows = response.Flows;
            Balance = response.Bilanz;
            IsLoading = false;
        }
    }
}