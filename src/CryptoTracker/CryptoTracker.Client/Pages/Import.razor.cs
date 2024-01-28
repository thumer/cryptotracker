using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Xml.Linq;

namespace CryptoTracker.Client.Pages
{
    public partial class Import
    {
        private const int MAX_REQUEST_SIZE = 1024 * 1024 * 100; //100MB
        private IDictionary<ImportDocumentType, IBrowserFile> _selectedFiles = new Dictionary<ImportDocumentType, IBrowserFile>();

        private string? ErrorMessage { get; set; }
        private Dictionary<ImportDocumentType, string> WalletNameDictionary = Enum.GetValues(typeof(ImportDocumentType)).OfType<ImportDocumentType>().ToDictionary(t => t, t => string.Empty);
        private Dictionary<ImportDocumentType, Guid> InputFileIdDictionary = Enum.GetValues(typeof(ImportDocumentType)).OfType<ImportDocumentType>().ToDictionary(t => t, t => Guid.NewGuid());

        private async Task HandleFileSelected(ImportDocumentType documentType, InputFileChangeEventArgs e)
        {
            _selectedFiles[documentType] = e.File;

            int indexOfHashtag = e.File.Name.IndexOf('#');
            if (indexOfHashtag >= 0)
            {
                int indexOfLastDot = e.File.Name.LastIndexOf('.');
                string walletName = e.File.Name.Substring(indexOfHashtag + 1, (e.File.Name.Length - (indexOfHashtag + 1)) - (e.File.Name.Length - indexOfLastDot));
                WalletNameDictionary[documentType] = walletName;
            }
        }

        private async Task UploadDocument(ImportDocumentType documentType)
        {
            if (_selectedFiles.TryGetValue(documentType, out var selectedFile) && selectedFile != null)
            {
                WalletNameDictionary.TryGetValue(documentType, out var walletName);

                using var content = new MultipartFormDataContent();
                using var fileContent = new StreamContent(selectedFile.OpenReadStream(MAX_REQUEST_SIZE));

                fileContent.Headers.ContentType =
                    new MediaTypeHeaderValue(selectedFile.ContentType);

                content.Add(
                    content: fileContent,
                    name: "\"file\"",
                    fileName: selectedFile.Name);
                content.Add(JsonContent.Create(new ImportFileRequest() { Type = documentType, WalletName = walletName }), "request");

                var response = await HttpClient.PostAsync($"api/DataImport/ImportFile", content);

                if (!response.IsSuccessStatusCode)
                {
                    ErrorMessage = "Fehler beim Hochladen der Dokumente: " + response.Content.ReadAsStringAsync().Result;
                    return;
                }

                InputFileIdDictionary[documentType] = Guid.NewGuid();
                WalletNameDictionary[documentType] = string.Empty;
            }
        }
    }
}