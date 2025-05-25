using Microsoft.AspNetCore.Components.Forms;

namespace CryptoTracker.Shared;

public interface IDataImportApi
{
    Task ImportFileAsync(ImportDocumentType type, string walletName, IBrowserFile file);
    Task ProcessTransactionPairsAsync();
}
