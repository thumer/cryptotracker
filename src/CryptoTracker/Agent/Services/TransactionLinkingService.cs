using CryptoTracker.Agent.Common;
using CryptoTracker.Agent.Definitions;
using CryptoTracker.Entities;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Agent.Services;

/// <summary>
/// Service für KI-gestützte Transaktionsverknüpfung
/// </summary>
public class TransactionLinkingService
{
    private readonly AILinkingAgentBuilder _agentBuilder;
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly ILogger<TransactionLinkingService> _logger;

    public TransactionLinkingService(
        AILinkingAgentBuilder agentBuilder,
        CryptoTrackerDbContext dbContext,
        ILogger<TransactionLinkingService> logger)
    {
        _agentBuilder = agentBuilder;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Prüft ob der AI-Service konfiguriert ist
    /// </summary>
    public bool IsConfigured => _agentBuilder.IsConfigured();

    /// <summary>
    /// Startet eine neue Linking-Session
    /// </summary>
    public async Task<LinkingSessionResult> StartSessionAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return new LinkingSessionResult
            {
                AgentResponse = "AI-Service nicht konfiguriert. Bitte OpenAI-Einstellungen in appsettings.json prüfen."
            };
        }

        try
        {
            var agent = _agentBuilder.BuildAgent(TransactionLinkingAgentDefinition.KEY);

            var result = await agent.RunAsync(
                "Starte eine neue Verknüpfungs-Session. " +
                "Lade zuerst deine gespeicherten Regeln und dann die unverknüpften Transaktionen. " +
                "Gib mir einen Überblick über die Situation: Wie viele unverknüpfte Transaktionen gibt es? " +
                "Welche Symbole sind betroffen? Gibt es offensichtliche Muster?",
                cancellationToken: ct);

            return new LinkingSessionResult
            {
                AgentResponse = result.Text ?? "Session gestartet.",
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Starten der Agent-Session");
            return new LinkingSessionResult
            {
                AgentResponse = $"Fehler beim Starten der Session: {ex.Message}",
                Success = false
            };
        }
    }

    /// <summary>
    /// Setzt eine Konversation mit dem Agent fort
    /// </summary>
    public async Task<LinkingSessionResult> ContinueSessionAsync(
        string userMessage,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return new LinkingSessionResult
            {
                AgentResponse = "AI-Service nicht konfiguriert."
            };
        }

        try
        {
            var agent = _agentBuilder.BuildAgent(TransactionLinkingAgentDefinition.KEY);

            var result = await agent.RunAsync(userMessage, cancellationToken: ct);

            return new LinkingSessionResult
            {
                AgentResponse = result.Text ?? "",
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Fortsetzen der Agent-Session");
            return new LinkingSessionResult
            {
                AgentResponse = $"Fehler: {ex.Message}",
                Success = false
            };
        }
    }

    /// <summary>
    /// Führt automatische Verknüpfung durch (ohne Benutzerinteraktion)
    /// </summary>
    public async Task<AutoLinkResult> RunAutomaticLinkingAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return new AutoLinkResult
            {
                Summary = "AI-Service nicht konfiguriert.",
                Success = false
            };
        }

        try
        {
            var agent = _agentBuilder.BuildAgent(TransactionLinkingAgentDefinition.KEY);

            var startTime = DateTimeOffset.UtcNow;

            var result = await agent.RunAsync(
                """
                Führe automatische Verknüpfung durch:
                1. Lade gespeicherte Regeln (get_memory)
                2. Lade alle unverknüpften Transaktionen
                3. Für jede Transaktion:
                   - Prüfe ob bekannte Skip-Patterns zutreffen → mark_intentionally_unlinked
                   - Suche passende Gegenstücke (find_matching_transactions)
                   - Bei Konfidenz >= 0.9: Verknüpfe automatisch (link_transactions)
                4. Gib mir eine detaillierte Zusammenfassung:
                   - Wie viele wurden verknüpft?
                   - Wie viele als externe Einnahme markiert?
                   - Wie viele bleiben unverknüpft?
                   - Welche brauchen manuelle Prüfung?
                """,
                cancellationToken: ct);

            // Zähle Ergebnisse
            var linkedCount = await _dbContext.TransactionLinkMetadata
                .CountAsync(m => m.LinkedAt > startTime &&
                                !m.LinkType.HasFlag(TransactionLinkType.IntentionallyUnlinked), ct);

            var markedUnlinkedCount = await _dbContext.TransactionLinkMetadata
                .CountAsync(m => m.LinkedAt > startTime &&
                                m.LinkType.HasFlag(TransactionLinkType.IntentionallyUnlinked), ct);

            var remainingUnlinked = await _dbContext.CryptoTransactions
                .CountAsync(t => t.OppositeTransactionId == null && !t.IsIntentionallyUnlinked, ct);

            return new AutoLinkResult
            {
                LinkedCount = linkedCount,
                MarkedUnlinkedCount = markedUnlinkedCount,
                RemainingUnlinkedCount = remainingUnlinked,
                Summary = result.Text ?? "",
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei automatischer Verknüpfung");
            return new AutoLinkResult
            {
                Summary = $"Fehler: {ex.Message}",
                Success = false
            };
        }
    }

    /// <summary>
    /// Gibt Statistiken über unverknüpfte Transaktionen zurück
    /// </summary>
    public async Task<LinkingStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        var totalTransactions = await _dbContext.CryptoTransactions.CountAsync(ct);

        var linkedTransactions = await _dbContext.CryptoTransactions
            .CountAsync(t => t.OppositeTransactionId != null, ct);

        var intentionallyUnlinked = await _dbContext.CryptoTransactions
            .CountAsync(t => t.IsIntentionallyUnlinked, ct);

        var unlinkedTransactions = await _dbContext.CryptoTransactions
            .CountAsync(t => t.OppositeTransactionId == null && !t.IsIntentionallyUnlinked, ct);

        var confirmedLinks = await _dbContext.TransactionLinkMetadata
            .CountAsync(m => m.IsConfirmed, ct);

        var unconfirmedLinks = await _dbContext.TransactionLinkMetadata
            .CountAsync(m => !m.IsConfirmed && !m.LinkType.HasFlag(TransactionLinkType.IntentionallyUnlinked), ct);

        var bySymbol = await _dbContext.CryptoTransactions
            .Where(t => t.OppositeTransactionId == null && !t.IsIntentionallyUnlinked)
            .GroupBy(t => t.Symbol)
            .Select(g => new { Symbol = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);

        return new LinkingStatistics
        {
            TotalTransactions = totalTransactions,
            LinkedTransactions = linkedTransactions,
            IntentionallyUnlinked = intentionallyUnlinked,
            UnlinkedTransactions = unlinkedTransactions,
            ConfirmedLinks = confirmedLinks,
            UnconfirmedLinks = unconfirmedLinks,
            UnlinkedBySymbol = bySymbol.ToDictionary(x => x.Symbol, x => x.Count)
        };
    }

    /// <summary>
    /// Setzt alle Verknüpfungen zurück
    /// </summary>
    public async Task<ResetResult> ResetAllLinksAsync(
        bool keepManualLinks = true,
        bool resetIntentionallyUnlinked = false,
        CancellationToken ct = default)
    {
        var resetCount = 0;

        // 1. Finde zu löschende Metadaten
        var metadataQuery = _dbContext.TransactionLinkMetadata.AsQueryable();

        if (keepManualLinks)
        {
            metadataQuery = metadataQuery.Where(m =>
                !(m.IsConfirmed && m.LinkType.HasFlag(TransactionLinkType.Manual)));
        }

        if (!resetIntentionallyUnlinked)
        {
            metadataQuery = metadataQuery.Where(m =>
                !m.LinkType.HasFlag(TransactionLinkType.IntentionallyUnlinked));
        }

        var metadataToDelete = await metadataQuery.ToListAsync(ct);
        var transactionIdsToReset = metadataToDelete.Select(m => m.TransactionId).Distinct().ToHashSet();

        // 2. Setze OppositeTransactionId/OppositeWalletId zurück
        var allLinkedTransactions = await _dbContext.CryptoTransactions
            .Where(t => t.OppositeTransactionId != null)
            .ToListAsync(ct);

        foreach (var tx in allLinkedTransactions)
        {
            var shouldReset = transactionIdsToReset.Contains(tx.Id) ||
                             (tx.OppositeTransactionId.HasValue && transactionIdsToReset.Contains(tx.OppositeTransactionId.Value));

            if (shouldReset)
            {
                tx.OppositeTransactionId = null;
                tx.OppositeWalletId = null;
                resetCount++;
            }
        }

        // 3. Setze IsIntentionallyUnlinked zurück
        if (resetIntentionallyUnlinked)
        {
            var intentionallyUnlinked = await _dbContext.CryptoTransactions
                .Where(t => t.IsIntentionallyUnlinked)
                .ToListAsync(ct);

            foreach (var tx in intentionallyUnlinked)
            {
                tx.IsIntentionallyUnlinked = false;
            }
        }

        // 4. Lösche Metadaten
        _dbContext.TransactionLinkMetadata.RemoveRange(metadataToDelete);

        await _dbContext.SaveChangesAsync(ct);

        return new ResetResult
        {
            ResetCount = resetCount,
            MetadataDeleted = metadataToDelete.Count
        };
    }
}

public record LinkingSessionResult
{
    public string AgentResponse { get; init; } = "";
    public bool Success { get; init; }
    public List<TransactionLinkProposal>? Proposals { get; init; }
}

public record TransactionLinkProposal
{
    public int SendId { get; init; }
    public int ReceiveId { get; init; }
    public decimal Confidence { get; init; }
    public string Reason { get; init; } = "";
}

public record AutoLinkResult
{
    public int LinkedCount { get; init; }
    public int MarkedUnlinkedCount { get; init; }
    public int RemainingUnlinkedCount { get; init; }
    public string Summary { get; init; } = "";
    public bool Success { get; init; }
}

public record LinkingStatistics
{
    public int TotalTransactions { get; init; }
    public int LinkedTransactions { get; init; }
    public int IntentionallyUnlinked { get; init; }
    public int UnlinkedTransactions { get; init; }
    public int ConfirmedLinks { get; init; }
    public int UnconfirmedLinks { get; init; }
    public Dictionary<string, int> UnlinkedBySymbol { get; init; } = new();
}

public record ResetResult
{
    public int ResetCount { get; init; }
    public int MetadataDeleted { get; init; }
}
