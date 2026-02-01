using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using CryptoTracker.Shared;
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
    private readonly ILinkingAgentContextAccessor _contextAccessor;

    public MarkAsIntentionallyUnlinkedTool(
        CryptoTrackerDbContext dbContext,
        ILinkingAgentContextAccessor contextAccessor)
    {
        _dbContext = dbContext;
        _contextAccessor = contextAccessor;
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
            .Include(t => t.Wallet)
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

        var context = _contextAccessor.Current;
        if (context != null)
        {
            foreach (var tx in transactions)
            {
                context.Session.MarkedExternalCount++;
                context.Session.ProcessedCount = context.Session.LinkedCount +
                                                context.Session.MarkedExternalCount +
                                                context.Session.SkippedCount;

                await context.SendEventAsync(new LinkingEventDTO
                {
                    EventType = "marked_external",
                    Message = $"Extern: {tx.Symbol} {tx.QuantityAfterFee:F8} - {reason}",
                    Transaction = new UnlinkedTransactionDTO
                    {
                        Id = tx.Id,
                        DateTime = tx.DateTime,
                        Type = tx.TransactionType.ToString(),
                        Symbol = tx.Symbol,
                        Quantity = tx.Quantity,
                        QuantityAfterFee = tx.QuantityAfterFee,
                        Comment = tx.Comment,
                        Address = tx.Address,
                        WalletName = tx.Wallet.Name,
                        TransactionId = tx.TransactionId,
                        Network = tx.Network
                    },
                    ProcessedCount = context.Session.ProcessedCount,
                    TotalCount = context.Session.TotalCount
                });
            }
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"{markedIds.Count} Transaktion(en) als bewusst unverknüpft markiert",
            markedIds,
            reason
        });
    }
}
