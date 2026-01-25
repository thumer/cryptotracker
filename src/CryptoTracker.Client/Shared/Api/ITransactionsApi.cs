namespace CryptoTracker.Shared;

public interface ITransactionsApi
{
    Task<IList<TransactionRowDTO>> GetTransactionsAsync(string? walletName, string? symbol);
    Task<FlowDetailsDTO?> GetTransactionDetailsAsync(FlowType flowType, int id);
}
