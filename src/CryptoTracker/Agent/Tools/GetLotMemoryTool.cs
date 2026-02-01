using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool zum Laden von Lot-Linking-Regeln aus dem Gedächtnis.
/// </summary>
public sealed class GetLotMemoryTool : IAgentTool
{
    private readonly CryptoTrackerDbContext _dbContext;

    public GetLotMemoryTool(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Delegate GetToolRunner() => GetMemoryAsync;
    public string GetToolName() => "get_lot_memory";
    public string GetToolDescription() => """
        Lädt gespeicherte Lot-Linking-Regeln.
        Parameter:
        - memoryType: Optional, filtert nach Art (SkipPattern, WalletMapping, etc.)
        """;

    private async Task<string> GetMemoryAsync(
        [Description("Optional: SkipPattern, WalletMapping, AddressMapping, UserDecision, ImportErrorPattern")] string? memoryType = null)
    {
        const string agentKey = "lot-linking";

        var query = _dbContext.AgentMemories
            .Where(m => m.AgentKey == agentKey);

        if (!string.IsNullOrEmpty(memoryType) && Enum.TryParse<AgentMemoryType>(memoryType, true, out var type))
        {
            query = query.Where(m => m.MemoryType == type);
        }

        var memories = await query
            .OrderByDescending(m => m.UsageCount)
            .ThenByDescending(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                MemoryType = m.MemoryType.ToString(),
                m.Key,
                m.Value,
                m.Description,
                CreatedAt = m.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                m.UsageCount
            })
            .ToListAsync();

        var summary = memories
            .GroupBy(m => m.MemoryType)
            .ToDictionary(g => g.Key, g => g.Count());

        return JsonSerializer.Serialize(new
        {
            totalCount = memories.Count,
            summary,
            memories
        }, new JsonSerializerOptions { WriteIndented = false });
    }
}
