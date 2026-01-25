namespace CryptoTracker.Shared;

public interface IOverviewApi
{
    Task<OverviewSummaryDTO> GetOverviewAsync();
}
