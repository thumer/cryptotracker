using CryptoTracker.Agent.Common;
using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool um Lots bei einem Transfer zuzuordnen.
/// </summary>
public sealed class TransferLotsTool : IAgentTool
{
    private const string AutoFifoToken = "AUTO_FIFO";
    private readonly LotService _lotService;
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly ILotLinkingAgentContextAccessor _contextAccessor;

    public TransferLotsTool(
        LotService lotService,
        CryptoTrackerDbContext dbContext,
        ILotLinkingAgentContextAccessor contextAccessor)
    {
        _lotService = lotService;
        _dbContext = dbContext;
        _contextAccessor = contextAccessor;
    }

    public Delegate GetToolRunner() => TransferLotsAsync;
    public string GetToolName() => "transfer_lots";
    public string GetToolDescription() => """
        Ordnet Lots bei einem Transfer zu.
        Parameter:
        - sendTransactionId: ID der Send-Transaktion
        - receiveTransactionId: ID der Receive-Transaktion
        - allocationsJson: JSON-Array mit { lotId, quantity } oder leer/"AUTO_FIFO" für FIFO
        """;

    private async Task<string> TransferLotsAsync(
        [Description("Send-Transaktion ID")] int sendTransactionId,
        [Description("Receive-Transaktion ID")] int receiveTransactionId,
        [Description("JSON-Array {lotId, quantity}")] string allocationsJson)
    {
        List<LotAllocation> allocations;
        if (ShouldUseAutoFifo(allocationsJson))
        {
            var autoResult = await BuildAutoFifoAllocationsForTransferAsync(sendTransactionId, receiveTransactionId);
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

        var resultingLots = await _lotService.TransferLotsAsync(sendTransactionId, receiveTransactionId, allocations);

        var context = _contextAccessor.Current;
        if (context != null)
        {
            context.Session.AssignedCount++;
            context.Session.CreatedLotsCount += resultingLots.Count;
            context.Session.ProcessedCount = context.Session.AssignedCount + context.Session.SkippedCount;

            await context.SendEventAsync(new LotLinkingEventDTO
            {
                EventType = "assigned",
                Message = $"Transfer: {resultingLots.Count} Lot(s) erstellt und zugeordnet",
                ProcessedCount = context.Session.ProcessedCount,
                TotalCount = context.Session.TotalCount
            });
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            createdLots = resultingLots.Count
        });
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

    private async Task<AutoFifoResult> BuildAutoFifoAllocationsForTransferAsync(
        int sendTransactionId,
        int receiveTransactionId)
    {
        var send = await _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.Id == sendTransactionId);
        if (send == null)
        {
            return new AutoFifoResult();
        }

        var receive = await _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.Id == receiveTransactionId);
        if (receive == null)
        {
            return new AutoFifoResult();
        }

        var requiredQuantity = receive.QuantityAfterFee > 0 ? receive.QuantityAfterFee : send.QuantityAfterFee;
        if (requiredQuantity <= 0)
        {
            return new AutoFifoResult();
        }

        var maxDate = receive.DateTime;
        return await BuildAutoFifoAllocationsAsync(send.WalletId, send.Symbol, requiredQuantity, maxDate);
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

    private sealed class AutoFifoResult
    {
        public List<LotAllocation> Allocations { get; init; } = new();
        public decimal MissingQuantity { get; init; }
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

    private sealed record AllocationInput(int LotId, decimal Quantity);
}
