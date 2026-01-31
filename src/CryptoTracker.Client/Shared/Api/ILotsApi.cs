namespace CryptoTracker.Shared;

public interface ILotsApi
{
    // Lot-Abfragen
    Task<IList<LotDTO>> GetAvailableLotsAsync(string walletName, string symbol);
    Task<IList<LotDTO>> GetAltbestandLotsAsync(string walletName, string symbol);
    Task<IList<LotDTO>> GetNeubestandLotsAsync(string walletName, string symbol);
    Task<LotDTO?> GetLotByIdAsync(int lotId);
    Task<IDictionary<string, LotSummaryDTO>> GetLotSummaryByWalletAsync(string walletName);

    // Lot-Erstellung
    Task<LotDTO> CreateManualLotAsync(CreateManualLotRequest request);

    // Lot-Verwendung
    Task<IList<LotDTO>> TransferLotsAsync(TransferLotsRequest request);
    Task<SaleResultDTO> SellLotsAsync(SellLotsRequest request);

    // Auto-FIFO
    Task<FifoSuggestionDTO> SuggestFifoAllocationAsync(string walletName, string symbol, decimal quantity, bool prioritizeAltbestand);

    // Pending Assignments
    Task<IList<PendingLotAssignmentDTO>> GetPendingLotAssignmentsAsync();

    // Lot-Generierung
    Task<GenerateLotsResultDTO> GenerateLotsFromExistingDataAsync(GenerateLotsRequest request);
}
