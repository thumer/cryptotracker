using CryptoTracker.Shared;
using System.Net.Http.Json;

namespace CryptoTracker.Client.RestClients;

public class WalletRestClient : IWalletApi
{
    private readonly HttpClient _http;

    public WalletRestClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IList<WalletDTO>> GetWalletsAsync()
        => await _http.GetFromJsonAsync<IList<WalletDTO>>("api/Wallet/GetWallets") ?? new List<WalletDTO>();

    public async Task<IList<WalletInfoDTO>> GetWalletInfosAsync()
        => await _http.GetFromJsonAsync<IList<WalletInfoDTO>>("api/Wallet/GetWalletInfos") ?? new List<WalletInfoDTO>();

    public async Task<WalletInfoDTO> SaveWalletAsync(WalletInfoDTO wallet)
    {
        var response = await _http.PostAsJsonAsync("api/Wallet/SaveWallet", wallet);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WalletInfoDTO>() ?? wallet;
    }

    public async Task DeleteWalletAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/Wallet/{id}");
        response.EnsureSuccessStatusCode();
    }
}
