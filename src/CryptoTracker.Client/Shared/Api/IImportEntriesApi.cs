using System.Text.Json;

namespace CryptoTracker.Shared;

public interface IImportEntriesApi
{
    Task<IList<JsonElement>> GetEntriesAsync(ImportDocumentType type);
}
