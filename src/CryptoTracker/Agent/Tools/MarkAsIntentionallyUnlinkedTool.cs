using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool zum Markieren einer Transaktion als bewusst unverknüpft
/// </summary>
public sealed class MarkAsIntentionallyUnlinkedTool : IAgentTool
{
    private readonly CryptoTrackerDbContext _dbContext;

    public MarkAsIntentionallyUnlinkedTool(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Delegate GetToolRunner() => MarkAsIntentionallyUnlinkedAsync;
    public string GetToolName() => "mark_intentionally_unlinked";
    public string GetToolDescription() => """
        Markiert eine oder mehrere Transaktionen als bewusst ohne Gegenstück.
        Verwende dies für externe Einnahmen wie Staking Rewards, Airdrops, Mining, etc.
        
        Parameter:
        - transactionIds: Komma-separierte Liste von Datenbank-IDs (z.B. "1,2,3")
          Das sind die "Id" Felder aus get_unlinked_transactions, NICHT TxHash!
        - reason: Begründung warum kein Gegenstück existiert
        
        Gibt Anzahl der markierten Transaktionen zurück.
        """;

    private async Task<string> MarkAsIntentionallyUnlinkedAsync(
        [Description("Komma-separierte Liste von Datenbank-IDs, z.B. '1,2,3'")] string transactionIds,
        [Description("Begründung (z.B. 'Staking Rewards', 'Airdrop')")] string reason)
    {
        var ids = transactionIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        if (!ids.Any())
            return JsonSerializer.Serialize(new { success = false, error = "Keine gültigen IDs angegeben" });

        var transactions = await _dbContext.CryptoTransactions
            .Where(t => ids.Contains(t.Id))
            .Where(t => t.OppositeTransactionId == null) // Nur unverknüpfte
            .ToListAsync();

        if (!transactions.Any())
            return JsonSerializer.Serialize(new { success = false, error = "Keine passenden unverknüpften Transaktionen gefunden" });

        var markedIds = new List<int>();
        foreach (var tx in transactions)
        {
            tx.IsIntentionallyUnlinked = true;

            // Metadaten speichern
            var metadata = new TransactionLinkMetadata
            {
                TransactionId = tx.Id,
                LinkType = TransactionLinkType.IntentionallyUnlinked,
                Confidence = 1.0m,
                Reason = reason,
                LinkedAt = DateTimeOffset.UtcNow,
                IsConfirmed = true, // Bewusste Entscheidung = bestätigt
                ConfirmedAt = DateTimeOffset.UtcNow
            };
            _dbContext.TransactionLinkMetadata.Add(metadata);
            markedIds.Add(tx.Id);
        }

        await _dbContext.SaveChangesAsync();

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"{markedIds.Count} Transaktion(en) als bewusst unverknüpft markiert",
            markedIds,
            reason
        });
    }
}
