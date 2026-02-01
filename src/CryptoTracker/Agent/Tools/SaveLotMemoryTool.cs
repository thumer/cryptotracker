using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool zum Speichern von Lot-Linking-Regeln im Gedächtnis.
/// </summary>
public sealed class SaveLotMemoryTool : IAgentTool
{
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly ILotLinkingAgentContextAccessor _contextAccessor;

    public SaveLotMemoryTool(
        CryptoTrackerDbContext dbContext,
        ILotLinkingAgentContextAccessor contextAccessor)
    {
        _dbContext = dbContext;
        _contextAccessor = contextAccessor;
    }

    public Delegate GetToolRunner() => SaveMemoryAsync;
    public string GetToolName() => "save_lot_memory";
    public string GetToolDescription() => """
        Speichert eine Regel im Lot-Linking-Gedächtnis.
        Parameter:
        - memoryType: SkipPattern, WalletMapping, AddressMapping, UserDecision, ImportErrorPattern
        - key: Schlüssel
        - value: Wert
        - description: Beschreibung
        """;

    private async Task<string> SaveMemoryAsync(
        [Description("Art: SkipPattern, WalletMapping, AddressMapping, UserDecision, ImportErrorPattern")] string memoryType,
        [Description("Schlüssel")] string key,
        [Description("Wert")] string value,
        [Description("Beschreibung")] string description)
    {
        var context = _contextAccessor.Current;
        if (context == null || !context.AllowMemorySave)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Speichern nicht erlaubt (Benutzer hat 'Merken' nicht aktiviert)" });
        }

        if (!Enum.TryParse<AgentMemoryType>(memoryType, true, out var type))
            return JsonSerializer.Serialize(new { success = false, error = $"Ungültiger memoryType: {memoryType}" });

        if (string.IsNullOrWhiteSpace(key))
            return JsonSerializer.Serialize(new { success = false, error = "Key darf nicht leer sein" });

        const string agentKey = "lot-linking";

        var existing = await _dbContext.AgentMemories
            .FirstOrDefaultAsync(m => m.AgentKey == agentKey && m.MemoryType == type && m.Key == key);

        if (existing != null)
        {
            existing.Value = value;
            existing.Description = description;
            existing.UsageCount++;
        }
        else
        {
            var memory = new AgentMemory
            {
                AgentKey = agentKey,
                MemoryType = type,
                Key = key,
                Value = value,
                Description = description,
                CreatedAt = DateTimeOffset.UtcNow,
                UsageCount = 0
            };
            _dbContext.AgentMemories.Add(memory);
        }

        await _dbContext.SaveChangesAsync();

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = existing != null ? $"Regel aktualisiert: {type} '{key}'" : $"Neue Regel gespeichert: {type} '{key}'",
            memoryType = type.ToString(),
            key,
            value,
            isUpdate = existing != null
        });
    }
}
