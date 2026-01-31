using CryptoTracker.Client.Shared;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components;
using Radzen;

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
    private bool ShowHidden { get; set; } = false;

    private IList<TransactionRowDTO> Transactions { get; set; } = new List<TransactionRowDTO>();
    private bool IsDetailsOpen { get; set; }
    private bool IsDetailsLoading { get; set; }
    private string? DetailsError { get; set; }
    private FlowDetailsDTO? Details { get; set; }
    private FlowType? SelectedFlowType { get; set; }
    private int? SelectedFlowId { get; set; }
    private bool IsToggleHiddenBusy { get; set; }

    [Inject] public NavigationManager NavigationManager { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await LoadWalletsAsync();

        var walletFromQuery = GetQueryValue("wallet")?.Trim();
        var coinFromQuery = GetQueryValue("coin")?.Trim();
        var showHiddenFromQuery = GetQueryValue("showHidden")?.Trim();
        SelectedWalletName = string.IsNullOrWhiteSpace(walletFromQuery) ? null : walletFromQuery;
        SelectedCoinSymbol = string.IsNullOrWhiteSpace(coinFromQuery) ? null : coinFromQuery;
        ShowHidden = ParseBool(showHiddenFromQuery, ShowHidden);

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
            Transactions = await TransactionsApi.GetTransactionsAsync(SelectedWalletName, SelectedCoinSymbol, ShowHidden);
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

    private async Task OnShowHiddenChanged(ChangeEventArgs args)
    {
        ShowHidden = ParseBool(args.Value, ShowHidden);
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
        if (!ShowHidden)
            query.Add("showHidden=false");

        var suffix = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        NavigationManager.NavigateTo($"transaktionen{suffix}", replace: true);
    }

    private async Task OpenDetailsAsync(TransactionRowDTO row)
    {
        IsDetailsOpen = true;
        IsDetailsLoading = true;
        DetailsError = null;
        Details = null;
        SelectedFlowType = row.FlowType;
        SelectedFlowId = row.FlowId;
        try
        {
            Details = await TransactionsApi.GetTransactionDetailsAsync(row.FlowType, row.FlowId, ShowHidden);
            if (Details == null)
            {
                DetailsError = "Keine Details gefunden.";
            }
        }
        catch (Exception ex)
        {
            DetailsError = ex.Message;
        }
        IsDetailsLoading = false;
    }

    private void CloseDetails()
    {
        IsDetailsOpen = false;
        Details = null;
        DetailsError = null;
        SelectedFlowType = null;
        SelectedFlowId = null;
        IsToggleHiddenBusy = false;
    }

    private bool CanToggleHidden => CurrentHidden.HasValue && SelectedFlowType.HasValue && SelectedFlowId.HasValue;

    private bool IsCurrentHidden => CurrentHidden ?? false;

    private bool? CurrentHidden => Details?.FlowType switch
    {
        FlowType.Trade => Details?.Trade?.IsHidden,
        FlowType.Transaction => Details?.Transaction?.IsHidden,
        _ => null
    };

    private async Task ToggleHiddenAsync()
    {
        if (!SelectedFlowType.HasValue || !SelectedFlowId.HasValue || CurrentHidden == null)
            return;

        IsToggleHiddenBusy = true;
        DetailsError = null;

        try
        {
            var newHiddenState = !CurrentHidden.Value;
            var success = await TransactionsApi.SetHiddenAsync(new SetHiddenRequest(SelectedFlowType.Value, SelectedFlowId.Value, newHiddenState));
            if (!success)
            {
                DetailsError = "Eintrag konnte nicht aktualisiert werden.";
            }
            else
            {
                UpdateCurrentHiddenState(newHiddenState);
                await LoadTransactionsAsync();
            }
        }
        catch (Exception ex)
        {
            DetailsError = ex.Message;
        }
        IsToggleHiddenBusy = false;
    }

    private void UpdateCurrentHiddenState(bool isHidden)
    {
        if (Details == null)
            return;

        if (Details.FlowType == FlowType.Trade && Details.Trade != null)
        {
            Details = Details with { Trade = Details.Trade with { IsHidden = isHidden } };
        }
        else if (Details.FlowType == FlowType.Transaction && Details.Transaction != null)
        {
            Details = Details with { Transaction = Details.Transaction with { IsHidden = isHidden } };
        }
    }

    private void OnRowRender(RowRenderEventArgs<TransactionRowDTO> args)
    {
        if (args.Data == null || !args.Data.IsHidden)
            return;

        var attributes = args.Attributes;
        if (attributes == null)
            return;

        if (attributes.TryGetValue("class", out var existing))
        {
            attributes["class"] = $"{existing} hidden-row";
        }
        else
        {
            attributes["class"] = "hidden-row";
        }
    }

    private static bool ParseBool(object? value, bool fallback)
    {
        if (value is bool boolValue)
            return boolValue;

        if (value is string stringValue)
        {
            return ParseBool(stringValue, fallback);
        }

        return fallback;
    }

    private static bool ParseBool(string? value, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        if (bool.TryParse(value, out var parsed))
            return parsed;

        return value.Trim() switch
        {
            "1" => true,
            "0" => false,
            "on" => true,
            "off" => false,
            _ => fallback
        };
    }
}
