namespace CryptoTracker.Shared;

public interface IImportOverviewApi
{
    Task<ImportOverviewDTO> GetOverviewAsync();
}
