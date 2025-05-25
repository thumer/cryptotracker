using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components.Forms;

namespace CryptoTracker.Client.Pages
{
    public partial class Import
    {
        private const int MAX_REQUEST_SIZE = 1024 * 1024 * 100; //100MB
        private IDictionary<ImportDocumentType, IBrowserFile> _selectedFiles = new Dictionary<ImportDocumentType, IBrowserFile>();

        private string? ErrorMessage { get; set; }
        private Dictionary<ImportDocumentType, string> WalletNameDictionary = Enum.GetValues(typeof(ImportDocumentType)).OfType<ImportDocumentType>().ToDictionary(t => t, t => string.Empty);
        private Dictionary<ImportDocumentType, Guid> InputFileIdDictionary = Enum.GetValues(typeof(ImportDocumentType)).OfType<ImportDocumentType>().ToDictionary(t => t, t => Guid.NewGuid());
        private IList<WalletInfoDTO> Wallets { get; set; } = new List<WalletInfoDTO>();

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            Wallets = await WalletApi.GetWalletInfosAsync();
        }

        private Task HandleFileSelected(ImportDocumentType documentType, InputFileChangeEventArgs e)
        {
            _selectedFiles[documentType] = e.File;

            int indexOfHashtag = e.File.Name.IndexOf('#');
            if (indexOfHashtag >= 0)
            {
                int indexOfLastDot = e.File.Name.LastIndexOf('.');
                string walletName = e.File.Name.Substring(indexOfHashtag + 1, (e.File.Name.Length - (indexOfHashtag + 1)) - (e.File.Name.Length - indexOfLastDot));
                WalletNameDictionary[documentType] = walletName;
            }
            return Task.CompletedTask;
        }

        private async Task UploadDocument(ImportDocumentType documentType)
        {
            if (_selectedFiles.TryGetValue(documentType, out var selectedFile) && selectedFile != null)
            {
                WalletNameDictionary.TryGetValue(documentType, out var walletName);

                try
                {
                    await DataImportApi.ImportFileAsync(documentType, walletName ?? string.Empty, selectedFile);
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Fehler beim Hochladen der Dokumente: " + ex.Message;
                    return;
                }

                InputFileIdDictionary[documentType] = Guid.NewGuid();
                WalletNameDictionary[documentType] = string.Empty;
            }
        }

        private async Task ProcessTransactionPairs()
        {
            try
            {
                await DataImportApi.ProcessTransactionPairsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Fehler beim Hochladen der Dokumente: " + ex.Message;
            }
        }
    }
}