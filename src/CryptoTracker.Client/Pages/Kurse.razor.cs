using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components;

namespace CryptoTracker.Client.Pages;

public partial class Kurse
{
    private bool IsLoading { get; set; } = true;
    private string? ErrorMessage { get; set; }
    private IList<CoinRateDTO> Rates { get; set; } = new List<CoinRateDTO>();

    private bool IsDialogOpen { get; set; }
    private string? DialogSymbol { get; set; }
    private decimal? DialogPrice { get; set; }
    private DateTime DialogDate { get; set; } = DateTime.UtcNow.Date;
    private string? DialogError { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await LoadRatesAsync();
    }

    private async Task LoadRatesAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Rates = await CoinRatesApi.GetCoinRatesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        IsLoading = false;
    }

    private void OpenDialog(CoinRateDTO rate)
    {
        DialogSymbol = rate.Symbol;
        DialogDate = DateTime.UtcNow.Date;
        DialogPrice = rate.CurrentRateEur ?? rate.PreviousCloseRateEur;
        DialogError = null;
        IsDialogOpen = true;
    }

    private void CloseDialog()
    {
        IsDialogOpen = false;
        DialogSymbol = null;
        DialogError = null;
    }

    private async Task SaveDialogAsync()
    {
        if (string.IsNullOrWhiteSpace(DialogSymbol) || DialogPrice == null || DialogPrice <= 0)
        {
            DialogError = "Bitte einen gültigen Kurs setzen.";
            return;
        }

        await CoinRatesApi.SetManualCoinRateAsync(new SetManualCoinRateRequest(DialogSymbol, DialogDate, DialogPrice.Value));
        CloseDialog();
        await LoadRatesAsync();
    }
}
