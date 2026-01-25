namespace CryptoTracker.Shared;

public class ImportAutoRequest
{
    public string WalletName { get; set; } = string.Empty;

    public ImportDocumentType? DocumentType { get; set; }
}
