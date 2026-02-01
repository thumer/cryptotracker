using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using CryptoTracker.Shared;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool zum Laden verfügbarer Lots für eine Zuordnung.
/// </summary>
public sealed class GetLotOptionsTool : IAgentTool
{
    private readonly CryptoTrackerDbContext _dbContext;

    public GetLotOptionsTool(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Delegate GetToolRunner() => GetLotOptionsAsync;
    public string GetToolName() => "get_lot_options";
    public string GetToolDescription() => """
        Lädt verfügbare Lots für ein Symbol (optional gefiltert nach Wallet).
        Parameter:
        - symbol: z.B. "BTC"
        - walletId: Optional, nur Lots dieses Wallets
        - maxAcquisitionDate: Optional, nur Lots mit AcquisitionDate <= Datum
        - limit: max Anzahl (default 200)
        """;

    private async Task<string> GetLotOptionsAsync(
        [Description("Symbol, z.B. BTC")] string symbol,
        [Description("Optional: WalletId")] int? walletId = null,
        [Description("Max Anzahl")] int limit = 200,
        [Description("Optional: Max AcquisitionDate (ISO)")] DateTimeOffset? maxAcquisitionDate = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return JsonSerializer.Serialize(new { error = "Symbol fehlt" });
        }

        limit = Math.Clamp(limit, 1, 500);

        var query = _dbContext.AssetLots
            .Include(l => l.CurrentWallet)
            .Where(l => l.Symbol == symbol.ToUpperInvariant())
            .Where(l => l.RemainingQuantity > 0);

        if (walletId.HasValue)
        {
            query = query.Where(l => l.CurrentWalletId == walletId.Value);
        }

        if (maxAcquisitionDate.HasValue)
        {
            query = query.Where(l => l.AcquisitionDate <= maxAcquisitionDate.Value);
        }

        var lots = await query
            .OrderBy(l => l.AcquisitionDate)
            .Take(limit)
            .ToListAsync();

        var options = new List<LotOptionDTO>();
        foreach (var lot in lots)
        {
            var rootId = await GetRootLotIdAsync(lot);
            var rootLabel = rootId == lot.Id ? $"Root #{rootId}" : $"Root #{rootId} → Lot #{lot.Id}";
            var altLabel = lot.IsAltbestand ? "Altbestand" : "Neubestand";

            options.Add(new LotOptionDTO
            {
                LotId = lot.Id,
                DisplayText = $"{rootLabel} | {lot.RemainingQuantity:F8} {lot.Symbol} | {altLabel} | {lot.AcquisitionDate:yyyy-MM-dd} | {lot.CurrentWallet.Name}",
                AvailableQuantity = lot.RemainingQuantity,
                AcquisitionDate = lot.AcquisitionDate,
                AcquisitionPriceEur = lot.AcquisitionPriceEur,
                IsAltbestand = lot.IsAltbestand,
                WalletName = lot.CurrentWallet.Name
            });
        }

        return JsonSerializer.Serialize(new
        {
            totalCount = options.Count,
            options
        }, new JsonSerializerOptions { WriteIndented = false });
    }

    private async Task<int> GetRootLotIdAsync(AssetLot lot)
    {
        var current = lot;
        while (current.ParentLotId.HasValue)
        {
            var parent = await _dbContext.AssetLots.FindAsync(current.ParentLotId.Value);
            if (parent == null)
            {
                break;
            }
            current = parent;
        }

        return current.Id;
    }
}
