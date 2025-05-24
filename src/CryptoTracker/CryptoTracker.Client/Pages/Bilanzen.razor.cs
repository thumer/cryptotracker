using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace CryptoTracker.Client.Pages;

public partial class Bilanzen
{
    private bool IsLoading { get; set; } = true;
    private string? ErrorMessage { get; set; }
    private IList<PlatformBalanceDTO>? Balances { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        try
        {
            Balances = await HttpClient.GetFromJsonAsync<IList<PlatformBalanceDTO>>("api/Balance/GetBalances");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        IsLoading = false;
    }
}
