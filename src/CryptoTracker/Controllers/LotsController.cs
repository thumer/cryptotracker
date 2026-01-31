using CryptoTracker.Agent.Services;
using CryptoTracker.Entities;
using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LotsController : ControllerBase, ILotsApi
{
    private readonly LotService _lotService;
    private readonly LotFlowValidator _flowValidator;
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly InteractiveLotLinkingService _lotLinkingService;

    public LotsController(
        LotService lotService, 
        LotFlowValidator flowValidator, 
        CryptoTrackerDbContext dbContext,
        InteractiveLotLinkingService lotLinkingService)
    {
        _lotService = lotService;
        _flowValidator = flowValidator;
        _dbContext = dbContext;
        _lotLinkingService = lotLinkingService;
    }

    #region Hilfsmethoden

    private async Task<int?> GetWalletIdByNameAsync(string walletName)
    {
        var wallet = await _dbContext.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Name == walletName);
        return wallet?.Id;
    }

    #endregion

    #region Lot-Abfragen

    [HttpGet("GetAvailableLots")]
    public async Task<IList<LotDTO>> GetAvailableLots([FromQuery] string walletName, [FromQuery] string symbol)
    {
        var walletId = await GetWalletIdByNameAsync(walletName);
        if (walletId == null) return new List<LotDTO>();

        var lots = await _lotService.GetAvailableLotsAsync(walletId.Value, symbol);
        return lots.Select(MapToDTO).ToList();
    }

    [HttpGet("GetAltbestandLots")]
    public async Task<IList<LotDTO>> GetAltbestandLots([FromQuery] string walletName, [FromQuery] string symbol)
    {
        var walletId = await GetWalletIdByNameAsync(walletName);
        if (walletId == null) return new List<LotDTO>();

        var lots = await _lotService.GetAltbestandLotsAsync(walletId.Value, symbol);
        return lots.Select(MapToDTO).ToList();
    }

    [HttpGet("GetNeubestandLots")]
    public async Task<IList<LotDTO>> GetNeubestandLots([FromQuery] string walletName, [FromQuery] string symbol)
    {
        var walletId = await GetWalletIdByNameAsync(walletName);
        if (walletId == null) return new List<LotDTO>();

        var lots = await _lotService.GetNeubestandLotsAsync(walletId.Value, symbol);
        return lots.Select(MapToDTO).ToList();
    }

    [HttpGet("GetLotById")]
    public async Task<LotDTO?> GetLotById([FromQuery] int lotId)
    {
        var lot = await _lotService.GetLotByIdAsync(lotId);
        return lot != null ? MapToDTO(lot) : null;
    }

    [HttpGet("GetLotSummaryByWallet")]
    public async Task<IDictionary<string, LotSummaryDTO>> GetLotSummaryByWallet([FromQuery] string walletName)
    {
        var walletId = await GetWalletIdByNameAsync(walletName);
        if (walletId == null) return new Dictionary<string, LotSummaryDTO>();

        var summary = await _lotService.GetLotSummaryByWalletAsync(walletId.Value);
        return summary.ToDictionary(
            kvp => kvp.Key,
            kvp => new LotSummaryDTO(
                kvp.Value.Symbol,
                kvp.Value.TotalQuantity,
                kvp.Value.AltbestandQuantity,
                kvp.Value.NeubestandQuantity,
                kvp.Value.TotalAcquisitionCostEur,
                kvp.Value.AverageAcquisitionPrice,
                kvp.Value.LotCount));
    }

    #endregion

    #region Lot-Erstellung

    [HttpPost("CreateManualLot")]
    public async Task<LotDTO> CreateManualLot([FromBody] CreateManualLotRequest request)
    {
        var lot = await _lotService.CreateManualLotAsync(new ManualLotRequest
        {
            Symbol = request.Symbol,
            WalletId = request.WalletId,
            Quantity = request.Quantity,
            AcquisitionDate = request.AcquisitionDate,
            AcquisitionPriceEur = request.AcquisitionPriceEur,
            AcquisitionType = Enum.Parse<LotAcquisitionType>(request.AcquisitionType),
            SourceTransactionId = request.SourceTransactionId,
            Note = request.Note
        });
        return MapToDTO(lot);
    }

    #endregion

    #region Lot-Verwendung

    [HttpPost("TransferLots")]
    public async Task<IList<LotDTO>> TransferLots([FromBody] TransferLotsRequest request)
    {
        var allocations = request.Allocations
            .Select(a => new LotAllocation { LotId = a.LotId, Quantity = a.Quantity })
            .ToList();

        var lots = await _lotService.TransferLotsAsync(
            request.SendTransactionId,
            request.ReceiveTransactionId,
            allocations);

        return lots.Select(MapToDTO).ToList();
    }

    [HttpPost("SellLots")]
    public async Task<SaleResultDTO> SellLots([FromBody] SellLotsRequest request)
    {
        var allocations = request.Allocations
            .Select(a => new LotAllocation { LotId = a.LotId, Quantity = a.Quantity })
            .ToList();

        var result = await _lotService.SellLotsAsync(
            request.TradeId,
            allocations,
            request.SalePriceEurPerUnit);

        return new SaleResultDTO(
            result.TotalQuantity,
            result.TotalAcquisitionCost,
            result.TotalSaleProceeds,
            result.TotalRealizedGain,
            result.TaxFreeGain,
            result.TaxFreeQuantity,
            result.TaxableGain,
            result.TaxableQuantity,
            result.EstimatedKESt);
    }

    [HttpPost("TransformLotsViaSwap")]
    public async Task<LotDTO> TransformLotsViaSwap([FromBody] TransformLotsViaSwapRequest request)
    {
        var allocations = request.SourceAllocations
            .Select(a => new LotAllocation { LotId = a.LotId, Quantity = a.Quantity })
            .ToList();

        var newLot = await _lotService.TransformLotsViaSwapAsync(
            request.SellTradeId,
            request.BuyTradeId,
            allocations,
            request.ResultingQuantity);

        return MapToDTO(newLot);
    }

    #endregion

    #region Auto-FIFO

    [HttpGet("SuggestFifoAllocation")]
    public async Task<FifoSuggestionDTO> SuggestFifoAllocation(
        [FromQuery] string walletName,
        [FromQuery] string symbol,
        [FromQuery] decimal quantity,
        [FromQuery] bool prioritizeAltbestand = false)
    {
        var walletId = await GetWalletIdByNameAsync(walletName);
        if (walletId == null)
        {
            return new FifoSuggestionDTO(new List<LotAllocationDTO>(), 0, quantity, false);
        }

        var allocations = await _lotService.SuggestFifoAllocationAsync(
            walletId.Value, symbol, quantity, prioritizeAltbestand);

        var totalAllocated = allocations.Sum(a => a.Quantity);
        var missing = quantity - totalAllocated;

        return new FifoSuggestionDTO(
            allocations.Select(a => new LotAllocationDTO(a.LotId, a.Quantity)).ToList(),
            totalAllocated,
            missing > 0 ? missing : 0,
            missing <= 0);
    }

    #endregion

    #region Pending Assignments

    [HttpGet("GetPendingLotAssignments")]
    public async Task<IList<PendingLotAssignmentDTO>> GetPendingLotAssignments()
    {
        var transactions = await _lotService.GetTransactionsRequiringLotAssignmentAsync();
        var trades = await _lotService.GetTradesRequiringLotAssignmentAsync();

        var result = new List<PendingLotAssignmentDTO>();

        result.AddRange(transactions.Select(t => new PendingLotAssignmentDTO(
            "Transaction",
            t.Id,
            t.DateTime,
            t.Symbol,
            t.Quantity,
            t.Wallet.Name,
            t.WalletId,
            t.TransactionType.ToString(),
            t.OppositeWallet?.Name,
            t.Comment)));

        result.AddRange(trades.Select(t => new PendingLotAssignmentDTO(
            "Trade",
            t.Id,
            t.DateTime,
            t.Symbol,
            t.Quantity,
            t.Wallet.Name,
            t.WalletId,
            t.TradeType.ToString(),
            null,
            t.Comment)));

        return result.OrderBy(p => p.DateTime).ToList();
    }

    #endregion

    #region Lot-Generierung

    [HttpPost("GenerateLotsFromExistingData")]
    public async Task<GenerateLotsResultDTO> GenerateLotsFromExistingData([FromBody] GenerateLotsRequest request)
    {
        var count = 0;
        var errors = 0;

        if (request.FromTrades)
        {
            try
            {
                count += await _lotService.GenerateLotsFromExistingTradesAsync();
            }
            catch
            {
                errors++;
            }
        }

        return new GenerateLotsResultDTO(count, errors);
    }

    #endregion

    #region Flow-Validierung

    [HttpGet("ValidateLotFlow")]
    public async Task<LotFlowValidationDTO> ValidateLotFlow([FromQuery] int lotId)
    {
        var result = await _flowValidator.ValidateLotFlowAsync(lotId);
        return new LotFlowValidationDTO(
            result.LotId,
            result.Symbol,
            result.Quantity,
            result.AcquisitionDate,
            result.IsComplete,
            result.IncompleteReason,
            result.FlowChain.Select(s => new LotFlowStepDTO(
                s.LotId,
                s.Symbol,
                s.Quantity,
                s.Type.ToString(),
                s.TradeId,
                s.TransactionId,
                s.DateTime)).ToList(),
            result.EffectiveQuantity);
    }

    [HttpPost("RevalidateAllLotFlows")]
    public async Task<RevalidateFlowResultDTO> RevalidateAllLotFlows()
    {
        var updatedCount = await _flowValidator.ValidateAndUpdateAllLotsAsync();
        
        var completeCount = await _dbContext.AssetLots
            .Where(l => l.RemainingQuantity > 0 && l.IsFlowComplete)
            .CountAsync();
        var incompleteCount = await _dbContext.AssetLots
            .Where(l => l.RemainingQuantity > 0 && !l.IsFlowComplete)
            .CountAsync();

        return new RevalidateFlowResultDTO(updatedCount, completeCount, incompleteCount);
    }

    [HttpGet("GetLotsWithIncompleteFlow")]
    public async Task<IList<LotDTO>> GetLotsWithIncompleteFlow([FromQuery] string? walletName = null)
    {
        int? walletId = null;
        if (!string.IsNullOrWhiteSpace(walletName))
        {
            walletId = await GetWalletIdByNameAsync(walletName);
        }

        var lots = await _flowValidator.GetLotsWithIncompleteFlowAsync(walletId);
        return lots.Select(MapToDTO).ToList();
    }

    #endregion

    #region ILotsApi Implementation

    Task<IList<LotDTO>> ILotsApi.GetAvailableLotsAsync(string walletName, string symbol)
        => GetAvailableLots(walletName, symbol);

    Task<IList<LotDTO>> ILotsApi.GetAltbestandLotsAsync(string walletName, string symbol)
        => GetAltbestandLots(walletName, symbol);

    Task<IList<LotDTO>> ILotsApi.GetNeubestandLotsAsync(string walletName, string symbol)
        => GetNeubestandLots(walletName, symbol);

    Task<LotDTO?> ILotsApi.GetLotByIdAsync(int lotId)
        => GetLotById(lotId);

    Task<IDictionary<string, LotSummaryDTO>> ILotsApi.GetLotSummaryByWalletAsync(string walletName)
        => GetLotSummaryByWallet(walletName);

    Task<LotDTO> ILotsApi.CreateManualLotAsync(CreateManualLotRequest request)
        => CreateManualLot(request);

    Task<IList<LotDTO>> ILotsApi.TransferLotsAsync(TransferLotsRequest request)
        => TransferLots(request);

    Task<SaleResultDTO> ILotsApi.SellLotsAsync(SellLotsRequest request)
        => SellLots(request);

    Task<FifoSuggestionDTO> ILotsApi.SuggestFifoAllocationAsync(string walletName, string symbol, decimal quantity, bool prioritizeAltbestand)
        => SuggestFifoAllocation(walletName, symbol, quantity, prioritizeAltbestand);

    Task<IList<PendingLotAssignmentDTO>> ILotsApi.GetPendingLotAssignmentsAsync()
        => GetPendingLotAssignments();

    Task<GenerateLotsResultDTO> ILotsApi.GenerateLotsFromExistingDataAsync(GenerateLotsRequest request)
        => GenerateLotsFromExistingData(request);

    Task<InteractiveLotLinkingSessionDTO> ILotsApi.StartInteractiveLotLinkingSessionAsync()
        => StartInteractiveLotLinkingSession();

    Task ILotsApi.StopInteractiveLotLinkingSessionAsync(string sessionId)
        => StopInteractiveLotLinkingSession(sessionId);

    Task<IList<LotLinkingRuleDTO>> ILotsApi.GetLotLinkingRulesAsync()
        => GetLotLinkingRules();

    Task<bool> ILotsApi.DeleteLotLinkingRuleAsync(string ruleId)
        => DeleteLotLinkingRule(ruleId);

    Task<LotLinkingStatisticsDTO> ILotsApi.GetLotLinkingStatisticsAsync()
        => GetLotLinkingStatistics();

    #endregion

    #region Interactive Lot-Linking

    [HttpPost("StartInteractiveLotLinkingSession")]
    public async Task<InteractiveLotLinkingSessionDTO> StartInteractiveLotLinkingSession()
    {
        return await _lotLinkingService.StartSessionAsync();
    }

    [HttpPost("StopInteractiveLotLinkingSession")]
    public async Task StopInteractiveLotLinkingSession([FromQuery] string sessionId)
    {
        _lotLinkingService.StopSession(sessionId);
        await Task.CompletedTask;
    }

    [HttpGet("GetLotLinkingRules")]
    public async Task<IList<LotLinkingRuleDTO>> GetLotLinkingRules()
    {
        return await _lotLinkingService.GetLearnedRulesAsync();
    }

    [HttpDelete("DeleteLotLinkingRule")]
    public async Task<bool> DeleteLotLinkingRule([FromQuery] string ruleId)
    {
        return await _lotLinkingService.DeleteLearnedRuleAsync(ruleId);
    }

    [HttpGet("GetLotLinkingStatistics")]
    public async Task<LotLinkingStatisticsDTO> GetLotLinkingStatistics()
    {
        var pendingReceive = await _dbContext.CryptoTransactions
            .CountAsync(t => t.TransactionType == TransactionType.Receive && !t.LotAssignmentConfirmed);
        var pendingSell = await _dbContext.CryptoTrades
            .CountAsync(t => t.TradeType == TradeType.Sell && !t.LotAssignmentConfirmed);
        var completedTx = await _dbContext.CryptoTransactions
            .CountAsync(t => t.LotAssignmentConfirmed);
        var totalLots = await _dbContext.AssetLots.CountAsync();

        return new LotLinkingStatisticsDTO
        {
            TotalPendingAssignments = pendingReceive + pendingSell,
            PendingReceiveTransactions = pendingReceive,
            PendingSellTrades = pendingSell,
            CompletedAssignments = completedTx,
            LotsCreated = totalLots
        };
    }

    #endregion

    #region Mapping

    private static LotDTO MapToDTO(AssetLot lot) => new(
        lot.Id,
        lot.Symbol,
        lot.CurrentWalletId,
        lot.CurrentWallet?.Name ?? string.Empty,
        lot.RemainingQuantity,
        lot.OriginalQuantity,
        lot.AcquisitionDate,
        lot.AcquisitionPriceEur,
        lot.TotalAcquisitionCostEur,
        lot.AcquisitionType.ToString(),
        lot.IsAltbestand,
        lot.IsFullyConsumed,
        lot.Note,
        lot.ParentLotId,
        lot.SourceTradeId,
        lot.SourceTransactionId,
        lot.IsFlowComplete,
        lot.FlowIncompleteReason);

    #endregion
}
