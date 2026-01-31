namespace CryptoTracker.Shared;

public interface ITransactionsApi
{
    Task<IList<TransactionRowDTO>> GetTransactionsAsync(string? walletName, string? symbol, bool showHidden);
    Task<FlowDetailsDTO?> GetTransactionDetailsAsync(FlowType flowType, int id, bool showHidden);
    Task<bool> SetHiddenAsync(SetHiddenRequest request);
}
