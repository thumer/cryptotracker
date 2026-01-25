using CryptoTracker.Client.Shared;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components;

namespace CryptoTracker.Client.Pages;

public partial class Bilanzen
{
    private bool IsLoading { get; set; } = true;
    private string? ErrorMessage { get; set; }
    private IList<WalletDTO> Wallets { get; set; } = new List<WalletDTO>();
    private WalletBalanceDTO? SelectedBalance { get; set; }
    private string? SelectedWalletName { get; set; }
    private List<TopNavItem> WalletNavItems { get; set; } = new();

    [Inject] public NavigationManager NavigationManager { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await LoadWalletsAsync();
        var walletFromQuery = GetQueryValue("wallet")?.Trim();
        SelectedWalletName = !string.IsNullOrWhiteSpace(walletFromQuery)
            ? walletFromQuery
            : Wallets.FirstOrDefault()?.Name?.Trim();

        if (SelectedWalletName != null && !Wallets.Any(w => string.Equals(w.Name?.Trim(), SelectedWalletName, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedWalletName = Wallets.FirstOrDefault()?.Name?.Trim();
        }

        await LoadBalanceAsync();
    }

    private async Task LoadWalletsAsync()
    {
        try
        {
            Wallets = await WalletApi.GetWalletsAsync();
            WalletNavItems = Wallets
                .Select(w =>
                {
                    var name = w.Name?.Trim() ?? string.Empty;
                    return new TopNavItem(name, name);
                })
                .Where(w => !string.IsNullOrWhiteSpace(w.Value))
                .ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task LoadBalanceAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedWalletName))
        {
            IsLoading = false;
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            SelectedBalance = await BalanceApi.GetWalletBalanceAsync(SelectedWalletName);
            if (SelectedBalance == null)
            {
                ErrorMessage = "Wallet nicht gefunden.";
            }
            else
            {
                SelectedWalletName = SelectedBalance.WalletName?.Trim();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        IsLoading = false;
    }

    private async Task OnWalletChanged(string? walletName)
    {
        SelectedWalletName = walletName?.Trim();
        UpdateQuery(SelectedWalletName);
        await LoadBalanceAsync();
    }

    private string? GetQueryValue(string key)
    {
        var uri = new Uri(NavigationManager.Uri);
        if (string.IsNullOrWhiteSpace(uri.Query))
            return null;

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in query)
        {
            var kvp = part.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (kvp.Length != 2)
                continue;

            var name = Uri.UnescapeDataString(kvp[0]);
            if (!string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                continue;

            return Uri.UnescapeDataString(kvp[1]);
        }

        return null;
    }

    private void UpdateQuery(string? walletName)
    {
        if (string.IsNullOrWhiteSpace(walletName))
            return;

        NavigationManager.NavigateTo($"bilanzen?wallet={Uri.EscapeDataString(walletName)}", replace: true);
    }

    private void OnAssetSelected(AssetBalanceDetailDTO asset)
    {
        if (string.IsNullOrWhiteSpace(SelectedWalletName))
            return;

        NavigationManager.NavigateTo($"transaktionen?wallet={Uri.EscapeDataString(SelectedWalletName)}&coin={Uri.EscapeDataString(asset.Symbol)}");
    }
}
