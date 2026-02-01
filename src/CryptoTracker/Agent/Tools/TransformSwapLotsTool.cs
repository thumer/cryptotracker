using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool für Crypto-zu-Crypto Swap: Quell-Lots zuweisen und Ziel-Lot erzeugen.
/// </summary>
public sealed class TransformSwapLotsTool : IAgentTool
{
    private const string AutoFifoToken = "AUTO_FIFO";
    private readonly LotService _lotService;
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly ILotLinkingAgentContextAccessor _contextAccessor;

    public TransformSwapLotsTool(
        LotService lotService,
        CryptoTrackerDbContext dbContext,
        ILotLinkingAgentContextAccessor contextAccessor)
    {
        _lotService = lotService;
        _dbContext = dbContext;
        _contextAccessor = contextAccessor;
    }

    public Delegate GetToolRunner() => TransformSwapAsync;
    public string GetToolName() => "transform_swap_lots";
    public string GetToolDescription() => """
        Ordnet Lots bei einem Crypto-zu-Crypto Swap zu und erstellt das Ziel-Lot.
        Parameter:
        - sellTradeId: Sell-Trade ID (gibt Crypto ab)
        - buyTradeId: Buy-Trade ID (erhält Crypto)
        - resultingQuantity: Menge der erhaltenen Coins (nach Gebühren)
        - allocationsJson: JSON-Array mit { lotId, quantity } oder leer/"AUTO_FIFO" für FIFO
        """;

    private async Task<string> TransformSwapAsync(
        [Description("Sell-Trade ID")] int sellTradeId,
        [Description("Buy-Trade ID")] int buyTradeId,
        [Description("Erhaltene Menge (nach Gebühren)")] decimal resultingQuantity,
        [Description("JSON-Array {lotId, quantity}")] string allocationsJson)
    {
        List<LotAllocation> allocations;
        if (ShouldUseAutoFifo(allocationsJson))
        {
            var autoResult = await BuildAutoFifoAllocationsForSwapAsync(sellTradeId);
            if (autoResult.MissingQuantity > 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"FIFO unvollständig: es fehlen {autoResult.MissingQuantity:F8}"
                });
            }

            allocations = autoResult.Allocations;
        }
        else
        {
            allocations = ParseAllocations(allocationsJson);
        }

        if (allocations.Count == 0)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Keine gültigen Allocations" });
        }

        var lot = await _lotService.TransformLotsViaSwapAsync(
            sellTradeId,
            buyTradeId,
            allocations,
            resultingQuantity);

        var context = _contextAccessor.Current;
        if (context != null)
        {
            context.Session.AssignedCount++;
            context.Session.CreatedLotsCount++;
            context.Session.ProcessedCount = context.Session.AssignedCount + context.Session.SkippedCount;

            await context.SendEventAsync(new LotLinkingEventDTO
            {
                EventType = "lot_created",
                Message = $"Swap: Neues Lot #{lot.Id} erstellt ({lot.Symbol} {lot.OriginalQuantity:F8})",
                CreatedLot = MapToDto(lot),
                ProcessedCount = context.Session.ProcessedCount,
                TotalCount = context.Session.TotalCount
            });
        }

        return JsonSerializer.Serialize(new { success = true, lotId = lot.Id });
    }

    private static bool ShouldUseAutoFifo(string? allocationsJson)
    {
        if (string.IsNullOrWhiteSpace(allocationsJson))
        {
            return true;
        }

        var trimmed = allocationsJson.Trim();
        return trimmed.Equals(AutoFifoToken, StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("AUTO", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("FIFO", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<AutoFifoResult> BuildAutoFifoAllocationsForSwapAsync(int sellTradeId)
    {
        var trade = await _dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.Id == sellTradeId);
        if (trade == null || trade.TradeType != TradeType.Sell)
        {
            return new AutoFifoResult();
        }

        var requiredQuantity = trade.Quantity;
        if (requiredQuantity <= 0)
        {
            return new AutoFifoResult();
        }

        return await BuildAutoFifoAllocationsAsync(trade.WalletId, trade.Symbol, requiredQuantity, trade.DateTime);
    }

    private async Task<AutoFifoResult> BuildAutoFifoAllocationsAsync(
        int walletId,
        string symbol,
        decimal requiredQuantity,
        DateTimeOffset maxAcquisitionDate)
    {
        var lots = await _lotService.GetAvailableLotsAsync(walletId, symbol);
        var eligibleLots = lots
            .Where(l => l.AcquisitionDate <= maxAcquisitionDate)
            .OrderBy(l => l.AcquisitionDate)
            .ToList();

        var allocations = new List<LotAllocation>();
        var remaining = requiredQuantity;

        foreach (var lot in eligibleLots)
        {
            if (remaining <= 0)
            {
                break;
            }

            var toAllocate = Math.Min(lot.RemainingQuantity, remaining);
            if (toAllocate <= 0)
            {
                continue;
            }

            allocations.Add(new LotAllocation { LotId = lot.Id, Quantity = toAllocate });
            remaining -= toAllocate;
        }

        return new AutoFifoResult
        {
            Allocations = allocations,
            MissingQuantity = remaining > 0 ? remaining : 0m
        };
    }

    private static List<LotAllocation> ParseAllocations(string json)
    {
        var parsed = JsonSerializer.Deserialize<List<AllocationInput>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        }) ?? new List<AllocationInput>();
        return parsed
            .Where(a => a.LotId > 0 && a.Quantity > 0)
            .Select(a => new LotAllocation { LotId = a.LotId, Quantity = a.Quantity })
            .ToList();
    }

    private static LotDTO MapToDto(AssetLot lot) => new(
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

    private sealed record AllocationInput(int LotId, decimal Quantity);

    private sealed class AutoFifoResult
    {
        public List<LotAllocation> Allocations { get; init; } = new();
        public decimal MissingQuantity { get; init; }
    }
}
