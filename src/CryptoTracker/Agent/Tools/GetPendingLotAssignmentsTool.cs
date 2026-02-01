using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool zum Laden ausstehender Lot-Zuordnungen.
/// </summary>
public sealed class GetPendingLotAssignmentsTool : IAgentTool
{
    private readonly CryptoTrackerDbContext _dbContext;

    public GetPendingLotAssignmentsTool(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Delegate GetToolRunner() => GetPendingAssignmentsAsync;
    public string GetToolName() => "get_pending_assignments";
    public string GetToolDescription() => """
        Lädt alle ausstehenden Lot-Zuordnungen (Receive-Transaktionen + Sell-Trades).
        Parameter:
        - limit: max Anzahl (default 200)
        - offset: Startoffset (default 0)
        """;

    private async Task<string> GetPendingAssignmentsAsync(
        [Description("Max Anzahl")] int limit = 200,
        [Description("Offset")] int offset = 0)
    {
        limit = Math.Clamp(limit, 1, 500);
        offset = Math.Max(0, offset);

        var transactions = await _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Include(t => t.OppositeWallet)
            .Where(t => t.TransactionType == TransactionType.Receive && !t.LotAssignmentConfirmed)
            .Select(t => new PendingLotAssignmentDTO(
                "Transaction",
                t.Id,
                t.DateTime,
                t.Symbol,
                t.QuantityAfterFee,
                t.Wallet.Name,
                t.WalletId,
                "Receive",
                t.OppositeWallet != null ? t.OppositeWallet.Name : null,
                t.Comment)
            {
                OppositeTransactionId = t.OppositeTransactionId
            })
            .ToListAsync();

        var trades = await _dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .Where(t =>
                (t.TradeType == TradeType.Sell && !t.LotAssignmentConfirmed) ||
                (t.TradeType == TradeType.Buy
                 && t.ResultingLotId == null
                 && FiatSymbols.ForQuery.Contains(t.OppositeSymbol)))
            .Select(t => new PendingLotAssignmentDTO(
                "Trade",
                t.Id,
                t.DateTime,
                t.Symbol,
                t.TradeType == TradeType.Buy ? t.QuantityAfterFee : t.Quantity,
                t.Wallet.Name,
                t.WalletId,
                t.TradeType.ToString(),
                null,
                t.Comment)
            {
                OppositeTradeId = t.OppositeTradeId,
                OppositeSymbol = t.OppositeSymbol
            })
            .ToListAsync();

        var all = transactions
            .Concat(trades)
            .OrderBy(a => a.DateTime)
            .ToList();

        var totalCount = all.Count;
        var paged = all.Skip(offset).Take(limit).ToList();

        return JsonSerializer.Serialize(new
        {
            totalCount,
            offset,
            limit,
            assignments = paged
        }, new JsonSerializerOptions { WriteIndented = false });
    }
}
