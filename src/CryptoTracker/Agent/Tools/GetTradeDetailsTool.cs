using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool zum Laden von Trade-Details.
/// </summary>
public sealed class GetTradeDetailsTool : IAgentTool
{
    private readonly CryptoTrackerDbContext _dbContext;

    public GetTradeDetailsTool(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Delegate GetToolRunner() => GetTradeDetailsAsync;
    public string GetToolName() => "get_trade_details";
    public string GetToolDescription() => """
        Lädt Details zu einem oder mehreren Trades.
        Parameter:
        - tradeIds: Komma-separierte Liste von IDs (z.B. "1,2,3")
        """;

    private async Task<string> GetTradeDetailsAsync(
        [Description("Komma-separierte IDs")] string tradeIds)
    {
        var ids = tradeIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        if (!ids.Any())
        {
            return JsonSerializer.Serialize(new { error = "Keine gültigen IDs angegeben" });
        }

        var trades = await _dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .Where(t => ids.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                DateTime = t.DateTime.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                TradeType = t.TradeType.ToString(),
                t.Symbol,
                t.OppositeSymbol,
                t.Price,
                t.Quantity,
                t.QuantityAfterFee,
                t.Fee,
                WalletId = t.WalletId,
                WalletName = t.Wallet.Name,
                t.OppositeTradeId,
                t.Comment
            })
            .ToListAsync();

        return JsonSerializer.Serialize(trades, new JsonSerializerOptions { WriteIndented = false });
    }
}
