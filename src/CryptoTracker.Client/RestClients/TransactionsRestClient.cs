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

    public async Task<IList<TransactionRowDTO>> GetTransactionsAsync(string? walletName, string? symbol)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(walletName))
            query.Add($"walletName={Uri.EscapeDataString(walletName)}");
        if (!string.IsNullOrWhiteSpace(symbol))
            query.Add($"symbol={Uri.EscapeDataString(symbol)}");
        var suffix = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        return await _http.GetFromJsonAsync<IList<TransactionRowDTO>>($"api/Transactions/GetTransactions{suffix}") ?? new List<TransactionRowDTO>();
    }
}
