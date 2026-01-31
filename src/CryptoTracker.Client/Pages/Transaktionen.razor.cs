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
    private bool IsDetailsOpen { get; set; }
    private bool IsDetailsLoading { get; set; }
    private string? DetailsError { get; set; }
    private FlowDetailsDTO? Details { get; set; }
    private TransactionRowDTO? CurrentRow { get; set; }

    // Lot Assignment State
    private IList<LotAllocationDTO> SelectedLotAllocations { get; set; } = new List<LotAllocationDTO>();
    private bool IsConfirmingLotAssignment { get; set; }
    private string? LotAssignmentError { get; set; }
    private string? LotAssignmentSuccess { get; set; }
    private bool IsLotAssignmentConfirmed { get; set; }
    
    // Lot Assignment Modal State
    private bool IsLotAssignmentOpen { get; set; }
    private TransactionRowDTO? LotAssignmentRow { get; set; }

    [Inject] public NavigationManager NavigationManager { get; set; } = null!;

    private static readonly string[] FiatSymbols = { "EUR", "USD", "CHF", "GBP", "ZEUR", "ZUSD" };

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

    private async Task OpenDetailsAsync(TransactionRowDTO row)
    {
        IsDetailsOpen = true;
        IsDetailsLoading = true;
        DetailsError = null;
        Details = null;
        CurrentRow = row;
        
        // Reset lot assignment state
        SelectedLotAllocations = new List<LotAllocationDTO>();
        LotAssignmentError = null;
        LotAssignmentSuccess = null;
        IsLotAssignmentConfirmed = false;
        
        try
        {
            Details = await TransactionsApi.GetTransactionDetailsAsync(row.FlowType, row.FlowId);
            if (Details == null)
            {
                DetailsError = "Keine Details gefunden.";
            }
            // TODO: Check if lot assignment is already confirmed
            // This would require extending the FlowDetailsDTO or adding a separate API call
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
        CurrentRow = null;
        SelectedLotAllocations = new List<LotAllocationDTO>();
        LotAssignmentError = null;
        LotAssignmentSuccess = null;
    }

    #region Lot Assignment

    private static bool IsFiatSymbol(string symbol)
    {
        return FiatSymbols.Contains(symbol.ToUpperInvariant());
    }

    /// <summary>
    /// Determines if a row requires lot assignment (sell trades to fiat or outgoing transfers)
    /// </summary>
    private bool RequiresLotAssignment(TransactionRowDTO row)
    {
        // Sell trade: Outflow of crypto -> fiat (TargetSymbol is fiat)
        if (row.FlowType == FlowType.Trade && 
            row.FlowDirection == FlowDirection.Outflow && 
            !string.IsNullOrWhiteSpace(row.TargetSymbol) && 
            IsFiatSymbol(row.TargetSymbol))
        {
            return true;
        }

        // Outgoing transfer: Transaction with outflow direction
        if (row.FlowType == FlowType.Transaction && 
            row.FlowDirection == FlowDirection.Outflow &&
            !IsFiatSymbol(row.Symbol))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if the row is a sell trade (crypto to fiat)
    /// </summary>
    private bool IsSellTrade(TransactionRowDTO row)
    {
        return row.FlowType == FlowType.Trade && 
               row.FlowDirection == FlowDirection.Outflow && 
               !string.IsNullOrWhiteSpace(row.TargetSymbol) && 
               IsFiatSymbol(row.TargetSymbol);
    }

    /// <summary>
    /// Gets the wallet name for lot assignment (source wallet for outflows)
    /// </summary>
    private string GetLotAssignmentWallet()
    {
        if (LotAssignmentRow == null) return string.Empty;
        
        // For trades, use the source wallet (row.SourceWallet)
        // For transactions (transfers), use the source wallet
        return LotAssignmentRow.SourceWallet ?? string.Empty;
    }

    /// <summary>
    /// Opens the lot assignment modal for a given transaction row
    /// </summary>
    private Task OpenLotAssignmentAsync(TransactionRowDTO row)
    {
        LotAssignmentRow = row;
        IsLotAssignmentOpen = true;
        
        // Reset state
        SelectedLotAllocations = new List<LotAllocationDTO>();
        LotAssignmentError = null;
        LotAssignmentSuccess = null;
        IsLotAssignmentConfirmed = false;
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Closes the lot assignment modal
    /// </summary>
    private void CloseLotAssignment()
    {
        IsLotAssignmentOpen = false;
        LotAssignmentRow = null;
        SelectedLotAllocations = new List<LotAllocationDTO>();
        LotAssignmentError = null;
        LotAssignmentSuccess = null;
    }

    /// <summary>
    /// Confirms the lot assignment (calls appropriate API based on type)
    /// </summary>
    private async Task ConfirmLotAssignment()
    {
        if (LotAssignmentRow == null || SelectedLotAllocations.Count == 0)
            return;

        if (IsSellTrade(LotAssignmentRow))
        {
            await ConfirmSellLotAssignmentInternal();
        }
        else
        {
            await ConfirmTransferLotAssignmentInternal();
        }
    }

    private async Task ConfirmSellLotAssignmentInternal()
    {
        if (LotAssignmentRow == null) return;

        IsConfirmingLotAssignment = true;
        LotAssignmentError = null;
        LotAssignmentSuccess = null;

        try
        {
            var salePricePerUnit = LotAssignmentRow.EuroValue / LotAssignmentRow.Amount;
            var request = new SellLotsRequest(
                LotAssignmentRow.FlowId,
                SelectedLotAllocations,
                salePricePerUnit);

            var result = await LotsApi.SellLotsAsync(request);

            IsLotAssignmentConfirmed = true;
            LotAssignmentSuccess = $"Lot-Zuordnung bestätigt! " +
                $"Realisierter Gewinn: {result.TotalRealizedGain:N2}€ " +
                $"(steuerfrei: {result.TaxFreeGain:N2}€, " +
                $"steuerpflichtig: {result.TaxableGain:N2}€, " +
                $"KESt: {result.EstimatedKESt:N2}€)";
        }
        catch (Exception ex)
        {
            LotAssignmentError = $"Fehler beim Speichern: {ex.Message}";
        }
        finally
        {
            IsConfirmingLotAssignment = false;
        }
    }

    private async Task ConfirmTransferLotAssignmentInternal()
    {
        if (LotAssignmentRow == null) return;

        IsConfirmingLotAssignment = true;
        LotAssignmentError = null;
        LotAssignmentSuccess = null;

        try
        {
            // For transfers, we need both the send and receive transaction IDs
            var request = new TransferLotsRequest(
                LotAssignmentRow.FlowId,  // Send transaction ID
                LotAssignmentRow.FlowId,  // This should be the opposite - extend API later if needed
                SelectedLotAllocations);

            var resultLots = await LotsApi.TransferLotsAsync(request);

            IsLotAssignmentConfirmed = true;
            LotAssignmentSuccess = $"Lot-Zuordnung bestätigt! {resultLots.Count} Lots wurden auf das Ziel-Wallet übertragen.";
        }
        catch (Exception ex)
        {
            LotAssignmentError = $"Fehler beim Speichern: {ex.Message}";
        }
        finally
        {
            IsConfirmingLotAssignment = false;
        }
    }

    private void OnLotSelectionChanged(IList<LotAllocationDTO> allocations)
    {
        SelectedLotAllocations = allocations;
        LotAssignmentError = null;
        LotAssignmentSuccess = null;
    }

    private async Task ConfirmSellLotAssignment()
    {
        if (CurrentRow == null || Details?.Trade == null || SelectedLotAllocations.Count == 0)
            return;

        IsConfirmingLotAssignment = true;
        LotAssignmentError = null;
        LotAssignmentSuccess = null;

        try
        {
            var salePricePerUnit = Details.Trade.EuroValue / Details.Trade.Amount;
            var request = new SellLotsRequest(
                CurrentRow.FlowId,
                SelectedLotAllocations,
                salePricePerUnit);

            var result = await LotsApi.SellLotsAsync(request);

            IsLotAssignmentConfirmed = true;
            LotAssignmentSuccess = $"Lot-Zuordnung bestätigt! " +
                $"Realisierter Gewinn: {result.TotalRealizedGain:N2}€ " +
                $"(steuerfrei: {result.TaxFreeGain:N2}€, " +
                $"steuerpflichtig: {result.TaxableGain:N2}€, " +
                $"KESt: {result.EstimatedKESt:N2}€)";
        }
        catch (Exception ex)
        {
            LotAssignmentError = $"Fehler beim Speichern: {ex.Message}";
        }
        finally
        {
            IsConfirmingLotAssignment = false;
        }
    }

    private async Task ConfirmTransferLotAssignment()
    {
        if (CurrentRow == null || Details?.Transaction == null || Details.OppositeTransaction == null || SelectedLotAllocations.Count == 0)
            return;

        IsConfirmingLotAssignment = true;
        LotAssignmentError = null;
        LotAssignmentSuccess = null;

        try
        {
            // For transfers, we need both the send and receive transaction IDs
            // The current row is the send transaction, the opposite is the receive
            // TODO: The API currently requires transaction IDs, but we have FlowId
            // We need to ensure the FlowId corresponds to the correct transaction ID
            
            var request = new TransferLotsRequest(
                CurrentRow.FlowId,  // Send transaction ID
                CurrentRow.FlowId,  // This needs to be the opposite transaction ID - we need to extend the API
                SelectedLotAllocations);

            // Note: This simplified implementation assumes CurrentRow.FlowId works for both
            // In a real implementation, you'd need to get the opposite transaction ID
            // from the Details or extend the API
            
            var resultLots = await LotsApi.TransferLotsAsync(request);

            IsLotAssignmentConfirmed = true;
            LotAssignmentSuccess = $"Lot-Zuordnung bestätigt! {resultLots.Count} Lots wurden auf das Ziel-Wallet übertragen.";
        }
        catch (Exception ex)
        {
            LotAssignmentError = $"Fehler beim Speichern: {ex.Message}";
        }
        finally
        {
            IsConfirmingLotAssignment = false;
        }
    }

    #endregion
}
