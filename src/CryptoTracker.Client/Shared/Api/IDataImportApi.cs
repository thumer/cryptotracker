using Microsoft.AspNetCore.Components.Forms;

namespace CryptoTracker.Shared;

public interface IDataImportApi
{
    Task ImportFileAsync(ImportDocumentType type, string walletName, IBrowserFile file);
    Task<ImportPreviewResult> PreviewImportAsync(IBrowserFile file);
    Task ImportAutoAsync(string walletName, IBrowserFile file, ImportDocumentType? documentType = null);
    //Obsolete durch neue Linking Logic
    //Task ProcessTransactionPairsAsync();
}
