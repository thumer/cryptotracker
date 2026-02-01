using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool zum Speichern von Agent-Regeln im Gedächtnis
/// </summary>
public sealed class SaveAgentMemoryTool : IAgentTool
{
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly ILinkingAgentContextAccessor _contextAccessor;

    public SaveAgentMemoryTool(
        CryptoTrackerDbContext dbContext,
        ILinkingAgentContextAccessor contextAccessor)
    {
        _dbContext = dbContext;
        _contextAccessor = contextAccessor;
    }

    public Delegate GetToolRunner() => SaveAgentMemoryAsync;
    public string GetToolName() => "save_memory";
    public string GetToolDescription() => """
        Speichert eine Regel im Agent-Gedächtnis für zukünftige Verwendung.
        Verwende dies wenn der Benutzer eine wiederkehrende Entscheidung trifft.
        
        Parameter:
        - memoryType: Art des Eintrags:
          "SkipPattern" = Kommentar-Muster zum Überspringen (z.B. "Staking Rewards")
          "WalletMapping" = Wallet-Zuordnung (z.B. "PC-Wallet" → interner Transfer)
          "AddressMapping" = Adress-Zuordnung
          "UserDecision" = Allgemeine Benutzer-Entscheidung
          "ImportErrorPattern" = Erkanntes Importfehler-Muster
        - key: Schlüssel (z.B. das Kommentar-Pattern, die Adresse)
        - value: Wert (z.B. "skip", WalletId, Beschreibung)
        - description: Menschenlesbare Beschreibung
        
        Gibt Erfolg/Fehler zurück.
        """;

    private async Task<string> SaveAgentMemoryAsync(
        [Description("Art: SkipPattern, WalletMapping, AddressMapping, UserDecision, ImportErrorPattern")] string memoryType,
        [Description("Schlüssel (z.B. Kommentar-Pattern)")] string key,
        [Description("Wert (z.B. 'skip', WalletId)")] string value,
        [Description("Menschenlesbare Beschreibung")] string description)
    {
        var context = _contextAccessor.Current;
        if (context == null || !context.AllowMemorySave)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "Speichern nicht erlaubt (Benutzer hat 'Merken' nicht aktiviert)"
            });
        }

        if (!Enum.TryParse<AgentMemoryType>(memoryType, true, out var type))
            return JsonSerializer.Serialize(new { success = false, error = $"Ungültiger memoryType: {memoryType}" });

        if (string.IsNullOrWhiteSpace(key))
            return JsonSerializer.Serialize(new { success = false, error = "Key darf nicht leer sein" });

        const string agentKey = "transaction-linking";

        // Prüfen ob bereits existiert
        var existing = await _dbContext.AgentMemories
            .FirstOrDefaultAsync(m => m.AgentKey == agentKey && m.MemoryType == type && m.Key == key);

        if (existing != null)
        {
            // Update
            existing.Value = value;
            existing.Description = description;
            existing.UsageCount++;
        }
        else
        {
            // Insert
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
            message = existing != null
                ? $"Regel aktualisiert: {type} '{key}'"
                : $"Neue Regel gespeichert: {type} '{key}'",
            memoryType = type.ToString(),
            key,
            value,
            isUpdate = existing != null
        });
    }
}
