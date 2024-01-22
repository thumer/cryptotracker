using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Headers;
using System.Text;

namespace CryptoTracker.Client.Pages
{
    public partial class Import
    {
        private const int MAX_REQUEST_SIZE = 1024 * 1024 * 100; //100MB
        private IBrowserFile _selectedFile;

        private string? ErrorMessage { get; set; }
        private Guid InputFileId { get; set; } = Guid.NewGuid();

        private async Task HandleFileSelected(InputFileChangeEventArgs e)
        {
            _selectedFile = e.File;
        }

        private async Task UploadDocument()
        {
            if (_selectedFile != null)
            {
                using var content = new MultipartFormDataContent();
                using var fileContent = new StreamContent(_selectedFile.OpenReadStream(MAX_REQUEST_SIZE));

                fileContent.Headers.ContentType =
                    new MediaTypeHeaderValue(_selectedFile.ContentType);

                content.Add(
                    content: fileContent,
                    name: "\"file\"",
                    fileName: _selectedFile.Name);
                //content.Add(JsonContent.Create(new UpdateIndexRequest() { ContainerName = SelectedDocumentContainer, FileName = file.Name, Tags = Tags.ToArray() }), "request");

                var response = await HttpClient.PostAsync($"api/DataImport/ImportFile", content);

                if (!response.IsSuccessStatusCode)
                {
                    ErrorMessage = "Fehler beim Hochladen der Dokumente: " + response.Content.ReadAsStringAsync().Result;
                    return;
                }

                InputFileId = Guid.NewGuid();
            }
        }
    }
}
