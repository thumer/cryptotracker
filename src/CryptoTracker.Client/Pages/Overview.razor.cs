using CryptoTracker.Shared;
using CryptoTracker.Client.Shared;
using Microsoft.AspNetCore.Components;

namespace CryptoTracker.Client.Pages
{
    public partial class Overview
    {
        private bool IsLoading { get; set; } = true;
        private string? ErrorMessage { get; set; }
        private OverviewSummaryDTO? Summary { get; set; }

        [Inject] public NavigationManager NavigationManager { get; set; } = null!;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            try
            {
                Summary = await OverviewApi.GetOverviewAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }

            IsLoading = false;
        }

        private void NavigateToWallet(string walletName)
        {
            if (string.IsNullOrWhiteSpace(walletName))
                return;

            NavigationManager.NavigateTo($"bilanzen?wallet={Uri.EscapeDataString(walletName)}");
        }
    }
}
