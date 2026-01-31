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
    /// Führt automatische Verknüpfung durch - OHNE AI-Aufrufe!
    /// Verwendet regelbasierte Logik und vorberechnete Hints.
    /// </summary>
    public async Task<AutoLinkResult> RunAutomaticLinkingAsync(CancellationToken ct = default)
    {
        try
        {
            var startTime = DateTimeOffset.UtcNow;
            var linkedCount = 0;
            var markedUnlinkedCount = 0;
            var summaryLines = new List<string>();

            // 1. Lade gelernte Regeln
            var rules = await _dbContext.AgentMemories
                .Where(m => m.AgentKey == "transaction-linking")
                .ToListAsync(ct);

            var skipPatterns = rules
                .Where(r => r.MemoryType == AgentMemoryType.SkipPattern)
                .Select(r => r.Key.ToLowerInvariant())
                .ToHashSet();

            // 2. Lade alle unverknüpften Transaktionen
            var unlinked = await _dbContext.CryptoTransactions
                .Include(t => t.Wallet)
                .Where(t => t.OppositeTransactionId == null && !t.IsIntentionallyUnlinked)
                .OrderBy(t => t.DateTime)
                .ToListAsync(ct);

            if (unlinked.Count == 0)
            {
                return new AutoLinkResult
                {
                    LinkedCount = 0,
                    MarkedUnlinkedCount = 0,
                    RemainingUnlinkedCount = 0,
                    Summary = "Keine unverknüpften Transaktionen gefunden.",
                    Success = true
                };
            }

            var sends = unlinked.Where(t => t.TransactionType == TransactionType.Send).ToList();
            var receives = unlinked.Where(t => t.TransactionType == TransactionType.Receive).ToList();

            // 3. Automatische Verknüpfung basierend auf Regeln
            // Zuerst: Bekannte externe Einnahmen markieren
            foreach (var tx in receives.ToList())
            {
                var comment = tx.Comment?.ToLowerInvariant() ?? "";
                
                // Check gegen bekannte externe Muster
                var isExternal = IsKnownExternalTransaction(comment, skipPatterns);
                
                if (isExternal.matched)
                {
                    tx.IsIntentionallyUnlinked = true;
                    
                    var metadata = new TransactionLinkMetadata
                    {
                        TransactionId = tx.Id,
                        LinkType = TransactionLinkType.IntentionallyUnlinked | TransactionLinkType.AIAssisted,
                        Confidence = 1.0m,
                        Reason = isExternal.reason,
                        LinkedAt = DateTimeOffset.UtcNow,
                        IsConfirmed = true,
                        ConfirmedAt = DateTimeOffset.UtcNow
                    };
                    _dbContext.TransactionLinkMetadata.Add(metadata);
                    
                    receives.Remove(tx);
                    markedUnlinkedCount++;
                }
            }

            // 4. Verknüpfe Send/Receive Paare basierend auf Zeit + Betrag
            var linkedPairs = new List<(CryptoTransaction send, CryptoTransaction receive, string reason)>();
            
            foreach (var send in sends.ToList())
            {
                // Finde passendes Receive
                var bestMatch = receives
                    .Where(r => 
                        r.Symbol == send.Symbol &&
                        r.DateTime >= send.DateTime.AddMinutes(-5) &&
                        r.DateTime <= send.DateTime.AddHours(24))
                    .Select(r => new
                    {
                        Receive = r,
                        TimeDiff = r.DateTime - send.DateTime,
                        AmountDiff = Math.Abs(send.QuantityAfterFee - r.Quantity),
                        AmountPercent = send.QuantityAfterFee > 0 
                            ? Math.Abs(send.QuantityAfterFee - r.Quantity) / send.QuantityAfterFee 
                            : 1m
                    })
                    .Where(m => m.AmountPercent < 0.01m) // Max 1% Differenz
                    .OrderBy(m => m.AmountDiff)
                    .ThenBy(m => m.TimeDiff)
                    .FirstOrDefault();

                if (bestMatch != null)
                {
                    var reason = $"Auto-Link: {send.Symbol} {send.Quantity:F8}, " +
                                 $"Zeit: {bestMatch.TimeDiff.TotalMinutes:F0} Min, " +
                                 $"Δ: {bestMatch.AmountDiff:F8}";
                    
                    linkedPairs.Add((send, bestMatch.Receive, reason));
                    sends.Remove(send);
                    receives.Remove(bestMatch.Receive);
                }
            }

            // 5. Führe die Verknüpfungen durch
            foreach (var (send, receive, reason) in linkedPairs)
            {
                send.OppositeTransactionId = receive.Id;
                send.OppositeWalletId = receive.WalletId;
                receive.OppositeTransactionId = send.Id;
                receive.OppositeWalletId = send.WalletId;

                var metadata = new TransactionLinkMetadata
                {
                    TransactionId = send.Id,
                    LinkType = TransactionLinkType.AIAssisted,
                    Confidence = 0.95m,
                    Reason = reason,
                    LinkedAt = DateTimeOffset.UtcNow,
                    IsConfirmed = true,
                    ConfirmedAt = DateTimeOffset.UtcNow
                };
                _dbContext.TransactionLinkMetadata.Add(metadata);
                
                linkedCount++;
            }

            await _dbContext.SaveChangesAsync(ct);

            // 6. Statistiken berechnen
            var remainingUnlinked = sends.Count + receives.Count;

            // Zusammenfassung erstellen
            summaryLines.Add($"✅ {linkedCount} Transaktionen automatisch verknüpft");
            if (markedUnlinkedCount > 0)
                summaryLines.Add($"📥 {markedUnlinkedCount} als externe Einnahme markiert");
            if (remainingUnlinked > 0)
                summaryLines.Add($"⏳ {remainingUnlinked} benötigen manuelle Prüfung");

            // Details zu verknüpften Paaren
            if (linkedPairs.Count > 0)
            {
                var bySymbol = linkedPairs.GroupBy(p => p.send.Symbol)
                    .Select(g => $"{g.Key}: {g.Count()}")
                    .ToList();
                summaryLines.Add($"Verknüpft nach Symbol: {string.Join(", ", bySymbol)}");
            }

            return new AutoLinkResult
            {
                LinkedCount = linkedCount,
                MarkedUnlinkedCount = markedUnlinkedCount,
                RemainingUnlinkedCount = remainingUnlinked,
                Summary = string.Join("\n", summaryLines),
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
    /// Prüft ob eine Transaktion basierend auf Kommentar als externe Einnahme erkannt wird
    /// </summary>
    private (bool matched, string reason) IsKnownExternalTransaction(string comment, HashSet<string> customPatterns)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return (false, "");

        // Eingebaute Muster für externe Einnahmen
        var builtInPatterns = new Dictionary<string, string>
        {
            { "staking", "Staking Rewards" },
            { "eth 2.0 staking", "ETH 2.0 Staking Rewards" },
            { "airdrop", "Airdrop" },
            { "bonus", "Bonus" },
            { "referral", "Referral Bonus" },
            { "mining", "Mining Rewards" },
            { "lending", "Lending Interest" },
            { "interest", "Interest" },
            { "cashback", "Cashback" },
            { "reward", "Reward" },
            { "div. käufe", "Diverse Käufe (Fiat)" },
            { "kauf", "Kauf (Fiat)" },
        };

        foreach (var (pattern, reason) in builtInPatterns)
        {
            if (comment.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return (true, reason);
        }

        // Benutzerdefinierte Muster
        foreach (var pattern in customPatterns)
        {
            if (comment.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return (true, $"Benutzerdefiniert: {pattern}");
        }

        return (false, "");
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
