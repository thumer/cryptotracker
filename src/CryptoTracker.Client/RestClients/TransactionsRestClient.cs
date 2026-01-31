using CryptoTracker.Shared;
using System.Net.Http.Json;

namespace CryptoTracker.Client.RestClients;

public class TransactionsRestClient : ITransactionsApi
{
    private readonly HttpClient _http;

    public TransactionsRestClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IList<TransactionRowDTO>> GetTransactionsAsync(string? walletName, string? symbol, bool showHidden)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(walletName))
            query.Add($"walletName={Uri.EscapeDataString(walletName)}");
        if (!string.IsNullOrWhiteSpace(symbol))
            query.Add($"symbol={Uri.EscapeDataString(symbol)}");
        query.Add($"showHidden={showHidden.ToString().ToLowerInvariant()}");
        var suffix = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        return await _http.GetFromJsonAsync<IList<TransactionRowDTO>>($"api/Transactions/GetTransactions{suffix}") ?? new List<TransactionRowDTO>();
    }

    public async Task<FlowDetailsDTO?> GetTransactionDetailsAsync(FlowType flowType, int id, bool showHidden)
    {
        var url = $"api/Transactions/GetTransactionDetails?flowType={flowType}&id={id}&showHidden={showHidden.ToString().ToLowerInvariant()}";
        return await _http.GetFromJsonAsync<FlowDetailsDTO>(url);
    }

    public async Task<bool> SetHiddenAsync(SetHiddenRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/Transactions/SetHidden", request);
        if (!response.IsSuccessStatusCode)
            return false;
        return await response.Content.ReadFromJsonAsync<bool?>() ?? false;
    }
}
