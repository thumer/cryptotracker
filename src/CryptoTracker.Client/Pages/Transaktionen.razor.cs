using CryptoTracker.Client.Shared;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components;

namespace CryptoTracker.Client.Pages;

public partial class Transaktionen
{
    private bool IsLoading { get; set; } = true;
    private string? ErrorMessage { get; set; }

    private IList<WalletWithSymbolsDTO> Wallets { get; set; } = new List<WalletWithSymbolsDTO>();
    private List<TopNavItem> WalletNavItems { get; set; } = new();
    private List<TopNavItem> CoinNavItems { get; set; } = new();

    private string? SelectedWalletName { get; set; }
    private string? SelectedCoinSymbol { get; set; }

    private IList<TransactionRowDTO> Transactions { get; set; } = new List<TransactionRowDTO>();

    [Inject] public NavigationManager NavigationManager { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await LoadWalletsAsync();

        var walletFromQuery = GetQueryValue("wallet")?.Trim();
        var coinFromQuery = GetQueryValue("coin")?.Trim();
        SelectedWalletName = string.IsNullOrWhiteSpace(walletFromQuery) ? null : walletFromQuery;
        SelectedCoinSymbol = string.IsNullOrWhiteSpace(coinFromQuery) ? null : coinFromQuery;

        if (SelectedWalletName != null && !Wallets.Any(w => string.Equals(w.Name?.Trim(), SelectedWalletName, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedWalletName = null;
        }

        BuildNavItems();
        await LoadTransactionsAsync();
    }

    private async Task LoadWalletsAsync()
    {
        try
        {
            Wallets = await WalletApi.GetWalletsWithSymbolsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void BuildNavItems()
    {
        WalletNavItems = new List<TopNavItem> { new("Alle", null) };
        WalletNavItems.AddRange(Wallets.Select(w =>
        {
            var name = w.Name?.Trim() ?? string.Empty;
            return new TopNavItem(name, name);
        }).Where(w => !string.IsNullOrWhiteSpace(w.Value)));

        var symbols = SelectedWalletName == null
            ? Wallets.SelectMany(w => w.Symbols)
            : Wallets.FirstOrDefault(w => string.Equals(w.Name?.Trim(), SelectedWalletName, StringComparison.OrdinalIgnoreCase))?.Symbols ?? Array.Empty<string>();

        var distinctSymbols = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        CoinNavItems = new List<TopNavItem> { new("Alle", null) };
        CoinNavItems.AddRange(distinctSymbols.Select(s => new TopNavItem(s, s)));

        if (SelectedCoinSymbol != null && !distinctSymbols.Contains(SelectedCoinSymbol, StringComparer.OrdinalIgnoreCase))
        {
            SelectedCoinSymbol = null;
        }
    }

    private async Task LoadTransactionsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Transactions = await TransactionsApi.GetTransactionsAsync(SelectedWalletName, SelectedCoinSymbol);
            if (SelectedWalletName != null)
            {
                var normalized = SelectedWalletName.Trim();
                if (Wallets.Any(w => string.Equals(w.Name?.Trim(), normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    SelectedWalletName = normalized;
                }
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
        BuildNavItems();
        UpdateQuery();
        await LoadTransactionsAsync();
    }

    private async Task OnCoinChanged(string? coin)
    {
        SelectedCoinSymbol = coin?.Trim();
        UpdateQuery();
        await LoadTransactionsAsync();
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

    private void UpdateQuery()
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(SelectedWalletName))
            query.Add($"wallet={Uri.EscapeDataString(SelectedWalletName)}");
        if (!string.IsNullOrWhiteSpace(SelectedCoinSymbol))
            query.Add($"coin={Uri.EscapeDataString(SelectedCoinSymbol)}");

        var suffix = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        NavigationManager.NavigateTo($"transaktionen{suffix}", replace: true);
    }
}
