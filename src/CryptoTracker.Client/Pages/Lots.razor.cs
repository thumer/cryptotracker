using CryptoTracker.Client.Shared;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components;

namespace CryptoTracker.Client.Pages;

public partial class Lots
{
    private bool IsLoading { get; set; } = true;
    private string? ErrorMessage { get; set; }
    private string? SuccessMessage { get; set; }

    private IList<WalletDTO> Wallets { get; set; } = new List<WalletDTO>();
    private List<TopNavItem> WalletNavItems { get; set; } = new();
    private string? SelectedWalletName { get; set; }

    private IDictionary<string, LotSummaryDTO> LotSummaries { get; set; } = new Dictionary<string, LotSummaryDTO>();
    private string? SelectedSymbol { get; set; }
    private IList<LotDTO> SymbolLots { get; set; } = new List<LotDTO>();
    private IList<PendingLotAssignmentDTO> PendingAssignments { get; set; } = new List<PendingLotAssignmentDTO>();

    // Generate Dialog
    private bool IsGenerateDialogOpen { get; set; }
    private bool IsGenerating { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            Wallets = await WalletApi.GetWalletsAsync();
            WalletNavItems = Wallets
                .Select(w => new TopNavItem(w.Name, w.Name))
                .ToList();

            if (Wallets.Count > 0 && string.IsNullOrEmpty(SelectedWalletName))
            {
                SelectedWalletName = Wallets[0].Name;
            }

            await LoadWalletDataAsync();
            await LoadPendingAssignmentsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Fehler beim Laden: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadWalletDataAsync()
    {
        if (string.IsNullOrEmpty(SelectedWalletName)) return;

        try
        {
            LotSummaries = await LotsApi.GetLotSummaryByWalletAsync(SelectedWalletName);
            SelectedSymbol = null;
            SymbolLots = new List<LotDTO>();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Fehler beim Laden der Lots: {ex.Message}";
        }
    }

    private async Task LoadPendingAssignmentsAsync()
    {
        try
        {
            PendingAssignments = await LotsApi.GetPendingLotAssignmentsAsync();
        }
        catch (Exception ex)
        {
            // Nicht kritisch - nur loggen
            Console.WriteLine($"Fehler beim Laden der ausstehenden Zuordnungen: {ex.Message}");
        }
    }

    private async Task OnWalletChanged(string? walletName)
    {
        SelectedWalletName = walletName;
        await LoadWalletDataAsync();
    }

    private async Task ShowLotsForSymbol(string symbol)
    {
        if (string.IsNullOrEmpty(SelectedWalletName)) return;

        SelectedSymbol = symbol;
        try
        {
            SymbolLots = await LotsApi.GetAvailableLotsAsync(SelectedWalletName, symbol);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Fehler beim Laden der Lots: {ex.Message}";
        }
    }

    private void OpenGenerateDialog()
    {
        IsGenerateDialogOpen = true;
    }

    private void CloseGenerateDialog()
    {
        IsGenerateDialogOpen = false;
    }

    private async Task GenerateLots()
    {
        IsGenerating = true;
        ErrorMessage = null;

        try
        {
            var result = await LotsApi.GenerateLotsFromExistingDataAsync(
                new GenerateLotsRequest(FromTrades: true, FromTransactions: false));

            SuccessMessage = $"{result.LotsCreated} Lots erfolgreich generiert!";
            IsGenerateDialogOpen = false;

            // Daten neu laden
            await LoadWalletDataAsync();
            await LoadPendingAssignmentsAsync();

            // Success-Message nach 5 Sekunden ausblenden
            _ = Task.Delay(5000).ContinueWith(_ =>
            {
                SuccessMessage = null;
                InvokeAsync(StateHasChanged);
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Fehler beim Generieren: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private void OpenAssignmentDialog(PendingLotAssignmentDTO item)
    {
        // TODO: Implementiere Dialog für manuelle Lot-Zuordnung
        // Für jetzt zeigen wir eine Info-Meldung
        SuccessMessage = $"Lot-Zuordnung für {item.Type} #{item.Id} - Feature in Entwicklung";
    }
}
