using System.Collections.Concurrent;
using CryptoTracker.Entities;
using CryptoTracker.Hubs;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Agent.Services;

/// <summary>
/// Service für interaktives KI-gestütztes Linking mit Human-in-the-Loop
/// </summary>
public class InteractiveLinkingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<LinkingHub> _hubContext;
    private readonly ILogger<InteractiveLinkingService> _logger;
    private const string VirtualWalletAction = "virtual_wallet";

    // Aktive Sessions
    private readonly ConcurrentDictionary<string, InteractiveLinkingSession> _sessions = new();

    public InteractiveLinkingService(
        IServiceScopeFactory scopeFactory,
        IHubContext<LinkingHub> hubContext,
        ILogger<InteractiveLinkingService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Startet eine neue interaktive Linking-Session
    /// </summary>
    public async Task<InteractiveLinkingSessionDTO> StartSessionAsync(CancellationToken ct = default)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var session = new InteractiveLinkingSession
        {
            SessionId = sessionId,
            StartedAt = DateTimeOffset.UtcNow,
            UserResponseChannel = new AsyncQueue<UserResponseDTO>()
        };

        _sessions[sessionId] = session;

        // Starte den Linking-Prozess im Hintergrund
        _ = Task.Run(() => RunLinkingProcessAsync(session, ct), ct);

        return session.ToDTO();
    }

    /// <summary>
    /// User gibt Antwort auf Agent-Frage
    /// </summary>
    public async Task SubmitUserResponseAsync(string sessionId, UserResponseDTO response)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            await session.UserResponseChannel.EnqueueAsync(response);
        }
    }

    /// <summary>
    /// Stoppt eine Session
    /// </summary>
    public void StopSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.CancellationSource.Cancel();
        }
    }

    /// <summary>
    /// Gibt den aktuellen Session-Status zurück
    /// </summary>
    public InteractiveLinkingSessionDTO? GetSessionStatus(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var session) ? session.ToDTO() : null;
    }

    /// <summary>
    /// Hauptprozess für interaktives Linking
    /// </summary>
    private async Task RunLinkingProcessAsync(InteractiveLinkingSession session, CancellationToken ct)
    {
        var linkedCt = CancellationTokenSource.CreateLinkedTokenSource(ct, session.CancellationSource.Token);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CryptoTrackerDbContext>();

            // 1. Lade alle Daten und berechne Hints
            var context = await BuildLinkingContextAsync(dbContext, linkedCt.Token);
            session.TotalCount = context.UnlinkedTransactions.Count;

            await SendSessionUpdateAsync(session);

            // 2. Verarbeite jede Transaktion
            var transactions = context.UnlinkedTransactions.ToList();
            var hintsLookup = BuildHintsLookup(context.Hints);
            
            // Lokale Liste von Regeln, die während der Session aktualisiert wird
            var activeRules = context.LearnedRules.ToList();

            foreach (var tx in transactions)
            {
                if (linkedCt.Token.IsCancellationRequested)
                    break;

                session.CurrentTransaction = tx;
                session.ProcessedCount++;

                // Prüfe ob gelernte Regel zutrifft (mit aktueller Liste!)
                var matchingRule = FindMatchingRule(tx, activeRules);
                if (matchingRule != null)
                {
                    await ApplyRuleAsync(dbContext, session, tx, matchingRule, linkedCt.Token);
                    
                    // Update usage count in active rules
                    var ruleToUpdate = activeRules.FirstOrDefault(r => r.Id == matchingRule.Id);
                    if (ruleToUpdate != null)
                    {
                        var index = activeRules.IndexOf(ruleToUpdate);
                        activeRules[index] = ruleToUpdate with { TimesApplied = ruleToUpdate.TimesApplied + 1 };
                    }
                    continue;
                }

                // Prüfe ob Hint mit hoher Konfidenz vorhanden
                var hints = hintsLookup.GetValueOrDefault(tx.Id, []);
                var highConfidenceHint = hints.FirstOrDefault(h => h.ConfidenceScore >= 0.9m);

                if (highConfidenceHint != null)
                {
                    // Automatisch verknüpfen
                    var otherTx = transactions.FirstOrDefault(t => 
                        t.Id == (tx.IsSend ? highConfidenceHint.ReceiveId : highConfidenceHint.SendId));
                    
                    if (otherTx != null)
                    {
                        await LinkTransactionsAsync(dbContext, session, tx, otherTx, highConfidenceHint.Reason, linkedCt.Token);
                        continue;
                    }
                }

                // Frage den User (mit allen Transaktionen für Kontext)
                var question = BuildQuestion(tx, hints, transactions, activeRules);
                await AskUserAsync(session, question, linkedCt.Token);

                // Warte auf Antwort
                var response = await session.UserResponseChannel.DequeueAsync(linkedCt.Token);
                
                // Verarbeite Antwort und erhalte ggf. neue Regel zurück
                var newRule = await ProcessUserResponseAsync(dbContext, session, tx, hints, response, linkedCt.Token);
                
                // Wenn eine neue Regel gelernt wurde, zur aktiven Liste hinzufügen
                if (newRule != null)
                {
                    activeRules.Add(newRule);
                    _logger.LogInformation("Neue Regel zur aktiven Liste hinzugefügt: {Pattern} → {Action}", 
                        newRule.Pattern, newRule.Action);
                }
            }

            // 3. Session abschließen
            session.IsActive = false;
            var finalStats = await GetStatisticsAsync(dbContext, linkedCt.Token);

            await _hubContext.Clients.Group(session.SessionId)
                .SendAsync("OnSessionCompleted", finalStats, linkedCt.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Session {SessionId} wurde abgebrochen", session.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler in Session {SessionId}", session.SessionId);
            await _hubContext.Clients.Group(session.SessionId)
                .SendAsync("OnError", ex.Message, CancellationToken.None);
        }
        finally
        {
            _sessions.TryRemove(session.SessionId, out _);
        }
    }

    /// <summary>
    /// Baut den Linking-Context mit allen Daten und Hints
    /// </summary>
    private async Task<LinkingContextDTO> BuildLinkingContextAsync(CryptoTrackerDbContext dbContext, CancellationToken ct)
    {
        // Lade unverknüpfte Transaktionen
        var unlinked = await dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Where(t => t.OppositeTransactionId == null && !t.IsIntentionallyUnlinked)
            .OrderBy(t => t.DateTime)
            .Select(t => new UnlinkedTransactionDTO
            {
                Id = t.Id,
                DateTime = t.DateTime,
                Type = t.TransactionType.ToString(),
                Symbol = t.Symbol,
                Quantity = t.Quantity,
                QuantityAfterFee = t.QuantityAfterFee,
                Comment = t.Comment,
                Address = t.Address,
                WalletName = t.Wallet.Name,
                TransactionId = t.TransactionId,
                Network = t.Network
            })
            .ToListAsync(ct);

        // Berechne Hints (ähnlich wie ProcessTransactionPairs)
        var hints = CalculateHints(unlinked);

        // Lade gelernte Regeln
        var rules = await LoadLearnedRulesAsync(dbContext, ct);

        // Statistiken
        var stats = await GetStatisticsAsync(dbContext, ct);

        return new LinkingContextDTO
        {
            UnlinkedTransactions = unlinked,
            Hints = hints,
            LearnedRules = rules,
            Statistics = stats
        };
    }

    /// <summary>
    /// Berechnet potentielle Verknüpfungs-Hints basierend auf Zeit und Betrag
    /// </summary>
    private List<LinkingHintDTO> CalculateHints(IList<UnlinkedTransactionDTO> transactions)
    {
        var hints = new List<LinkingHintDTO>();
        var sends = transactions.Where(t => t.IsSend).ToList();
        var receives = transactions.Where(t => t.IsReceive).ToList();

        foreach (var send in sends)
        {
            foreach (var receive in receives)
            {
                // Muss gleiches Symbol sein
                if (!string.Equals(send.Symbol, receive.Symbol, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Zeit-Differenz (max 24 Stunden)
                var timeDiff = receive.DateTime - send.DateTime;
                if (timeDiff < TimeSpan.FromMinutes(-5) || timeDiff > TimeSpan.FromHours(24))
                    continue;

                // Betrags-Differenz
                var amountDiff = Math.Abs(send.QuantityAfterFee - receive.Quantity);
                var amountPercent = send.QuantityAfterFee > 0 
                    ? amountDiff / send.QuantityAfterFee 
                    : 1;

                // Berechne Konfidenz
                var confidence = CalculateConfidence(send, receive, timeDiff, amountPercent);

                if (confidence >= 0.5m)
                {
                    hints.Add(new LinkingHintDTO
                    {
                        SendId = send.Id,
                        ReceiveId = receive.Id,
                        ConfidenceScore = confidence,
                        Reason = BuildHintReason(send, receive, timeDiff, amountDiff),
                        TimeDifference = timeDiff,
                        AmountDifference = amountDiff
                    });
                }
            }
        }

        return hints.OrderByDescending(h => h.ConfidenceScore).ToList();
    }

    private decimal CalculateConfidence(
        UnlinkedTransactionDTO send, 
        UnlinkedTransactionDTO receive,
        TimeSpan timeDiff,
        decimal amountPercentDiff)
    {
        var score = 0.5m; // Basis (gleiches Symbol)

        // Zeit-Score (0-0.25)
        if (timeDiff.TotalMinutes <= 5) score += 0.25m;
        else if (timeDiff.TotalMinutes <= 30) score += 0.20m;
        else if (timeDiff.TotalHours <= 1) score += 0.15m;
        else if (timeDiff.TotalHours <= 6) score += 0.10m;
        else score += 0.05m;

        // Betrags-Score (0-0.25)
        if (amountPercentDiff == 0) score += 0.25m;
        else if (amountPercentDiff < 0.001m) score += 0.20m;
        else if (amountPercentDiff < 0.01m) score += 0.15m;
        else if (amountPercentDiff < 0.05m) score += 0.10m;
        else score += 0.05m;

        // Kommentar-Hinweise
        var sendComment = send.Comment?.ToLowerInvariant() ?? "";
        var receiveComment = receive.Comment?.ToLowerInvariant() ?? "";
        var walletNames = new[] { "ledger", "metamask", "binance", "kraken", "coinbase", "exodus", "trust" };
        
        if (walletNames.Any(w => sendComment.Contains(w) || receiveComment.Contains(w)))
            score += 0.05m;

        // Gleiche Adresse/TransactionId-Muster
        if (!string.IsNullOrEmpty(send.Address) && !string.IsNullOrEmpty(receive.Address))
        {
            // Netzwerk-gleich könnte ein Hinweis sein
            if (string.Equals(send.Network, receive.Network, StringComparison.OrdinalIgnoreCase))
                score += 0.02m;
        }

        return Math.Min(1.0m, score);
    }

    private string BuildHintReason(
        UnlinkedTransactionDTO send,
        UnlinkedTransactionDTO receive,
        TimeSpan timeDiff,
        decimal amountDiff)
    {
        var reasons = new List<string>();

        if (timeDiff.TotalMinutes <= 5)
            reasons.Add("Zeit < 5 Min");
        else if (timeDiff.TotalMinutes <= 30)
            reasons.Add($"Zeit: {timeDiff.TotalMinutes:F0} Min");
        else
            reasons.Add($"Zeit: {timeDiff.TotalHours:F1}h");

        if (amountDiff == 0)
            reasons.Add("Betrag exakt");
        else
            reasons.Add($"Δ {amountDiff:F8} {send.Symbol}");

        reasons.Add($"{send.WalletName} → {receive.WalletName}");

        return string.Join(", ", reasons);
    }

    private Dictionary<int, List<LinkingHintDTO>> BuildHintsLookup(IList<LinkingHintDTO> hints)
    {
        var lookup = new Dictionary<int, List<LinkingHintDTO>>();
        
        foreach (var hint in hints)
        {
            if (!lookup.ContainsKey(hint.SendId))
                lookup[hint.SendId] = [];
            lookup[hint.SendId].Add(hint);

            if (!lookup.ContainsKey(hint.ReceiveId))
                lookup[hint.ReceiveId] = [];
            lookup[hint.ReceiveId].Add(hint);
        }

        return lookup;
    }

    private LearnedRuleDTO? FindMatchingRule(UnlinkedTransactionDTO tx, IList<LearnedRuleDTO> rules)
    {
        foreach (var rule in rules)
        {
            var matches = rule.RuleType switch
            {
                "comment_pattern" => !string.IsNullOrEmpty(tx.Comment) && 
                                    tx.Comment.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase),
                "wallet_pattern" => tx.WalletName.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase),
                "symbol_pattern" => tx.Symbol.Equals(rule.Pattern, StringComparison.OrdinalIgnoreCase),
                _ => false
            };

            if (matches)
                return rule;
        }

        return null;
    }

    private async Task ApplyRuleAsync(
        CryptoTrackerDbContext dbContext,
        InteractiveLinkingSession session,
        UnlinkedTransactionDTO tx,
        LearnedRuleDTO rule,
        CancellationToken ct)
    {
        if (rule.Action == "mark_external")
        {
            await MarkAsExternalAsync(dbContext, session, tx, rule.Description, ct);
        }
        // Weitere Aktionen können hier hinzugefügt werden
    }

    private async Task LinkTransactionsAsync(
        CryptoTrackerDbContext dbContext,
        InteractiveLinkingSession session,
        UnlinkedTransactionDTO tx1,
        UnlinkedTransactionDTO tx2,
        string reason,
        CancellationToken ct)
    {
        var send = tx1.IsSend ? tx1 : tx2;
        var receive = tx1.IsReceive ? tx1 : tx2;

        var sendEntity = await dbContext.CryptoTransactions.FindAsync([send.Id], ct);
        var receiveEntity = await dbContext.CryptoTransactions.FindAsync([receive.Id], ct);

        if (sendEntity == null || receiveEntity == null)
            return;

        sendEntity.OppositeTransactionId = receive.Id;
        sendEntity.OppositeWalletId = receiveEntity.WalletId;
        receiveEntity.OppositeTransactionId = send.Id;
        receiveEntity.OppositeWalletId = sendEntity.WalletId;

        var metadata = new TransactionLinkMetadata
        {
            TransactionId = send.Id,
            LinkType = TransactionLinkType.AIAssisted,
            Confidence = 0.9m,
            Reason = reason,
            LinkedAt = DateTimeOffset.UtcNow,
            IsConfirmed = true,
            ConfirmedAt = DateTimeOffset.UtcNow
        };
        dbContext.TransactionLinkMetadata.Add(metadata);

        await dbContext.SaveChangesAsync(ct);

        session.LinkedCount++;

        await SendEventAsync(session, new LinkingEventDTO
        {
            EventType = "linked",
            Message = $"Verknüpft: {send.Symbol} {send.Quantity:F8} ({send.WalletName} → {receive.WalletName})",
            SendId = send.Id,
            ReceiveId = receive.Id,
            ProcessedCount = session.ProcessedCount,
            TotalCount = session.TotalCount
        });
    }

    private async Task MarkAsExternalAsync(
        CryptoTrackerDbContext dbContext,
        InteractiveLinkingSession session,
        UnlinkedTransactionDTO tx,
        string reason,
        CancellationToken ct)
    {
        var entity = await dbContext.CryptoTransactions.FindAsync([tx.Id], ct);
        if (entity == null)
            return;

        entity.IsIntentionallyUnlinked = true;

        var metadata = new TransactionLinkMetadata
        {
            TransactionId = tx.Id,
            LinkType = TransactionLinkType.IntentionallyUnlinked | TransactionLinkType.AIAssisted,
            Confidence = 1.0m,
            Reason = reason,
            LinkedAt = DateTimeOffset.UtcNow,
            IsConfirmed = true,
            ConfirmedAt = DateTimeOffset.UtcNow
        };
        dbContext.TransactionLinkMetadata.Add(metadata);

        await dbContext.SaveChangesAsync(ct);

        session.MarkedExternalCount++;

        await SendEventAsync(session, new LinkingEventDTO
        {
            EventType = "marked_external",
            Message = $"Extern: {tx.Symbol} {tx.Quantity:F8} - {reason}",
            Transaction = tx,
            ProcessedCount = session.ProcessedCount,
            TotalCount = session.TotalCount
        });
    }

    private LinkingQuestionDTO BuildQuestion(
        UnlinkedTransactionDTO tx,
        List<LinkingHintDTO> hints,
        List<UnlinkedTransactionDTO> allTransactions,
        IList<LearnedRuleDTO> rules)
    {
        var options = new List<string>();
        var optionToHintIndex = new Dictionary<string, int>();
        var message = new System.Text.StringBuilder();

        message.AppendLine($"**{(tx.IsSend ? "Send" : "Receive")}:** {tx.Symbol} {tx.Quantity:F8}");
        message.AppendLine($"**Wallet:** {tx.WalletName}");
        message.AppendLine($"**Zeit:** {tx.DateTime:dd.MM.yyyy HH:mm}");
        
        if (!string.IsNullOrEmpty(tx.Comment))
            message.AppendLine($"**Kommentar:** {tx.Comment}");

        if (hints.Count > 0)
        {
            message.AppendLine();
            message.AppendLine("**Mögliche Verknüpfungen:**");
            
            var hintIndex = 0;
            foreach (var hint in hints.Take(3))
            {
                var matchId = tx.IsSend ? hint.ReceiveId : hint.SendId;
                var matchTx = allTransactions.FirstOrDefault(t => t.Id == matchId);
                
                string optionText;
                if (matchTx != null)
                {
                    // Zeige detaillierte Info über den Match-Kandidaten
                    var timeDiffText = FormatTimeDifference(hint.TimeDifference);
                    var amountDiffText = hint.AmountDifference == 0 
                        ? "exakt" 
                        : $"Δ {hint.AmountDifference:F8}";
                    
                    message.AppendLine($"  {hintIndex + 1}. {matchTx.WalletName} | {matchTx.DateTime:dd.MM.yy HH:mm} | {matchTx.Quantity:F8} {matchTx.Symbol} | {timeDiffText}, {amountDiffText} ({hint.ConfidenceScore:P0})");
                    
                    // Option mit lesbarem Text
                    optionText = $"→ {matchTx.WalletName} ({matchTx.DateTime:dd.MM.yy HH:mm})";
                }
                else
                {
                    optionText = $"Verknüpfen mit #{matchId} ({hint.ConfidenceScore:P0})";
                }
                
                options.Add(optionText);
                optionToHintIndex[optionText] = hintIndex;
                hintIndex++;
            }
        }

        options.Add("Gegenstück in virtuelles Wallet buchen");
        options.Add("Als externe Einnahme/Ausgabe markieren");
        options.Add("Überspringen (später bearbeiten)");

        return new LinkingQuestionDTO
        {
            QuestionId = Guid.NewGuid().ToString("N"),
            Message = message.ToString(),
            Options = options,
            Transaction = tx,
            Hints = hints.Take(3).ToList(),
            OptionToHintIndex = optionToHintIndex
        };
    }

    private static string FormatTimeDifference(TimeSpan diff)
    {
        var absDiff = diff.Duration();
        if (absDiff.TotalMinutes < 1) return "< 1 Min";
        if (absDiff.TotalMinutes < 60) return $"{absDiff.TotalMinutes:F0} Min";
        if (absDiff.TotalHours < 24) return $"{absDiff.TotalHours:F1}h";
        return $"{absDiff.TotalDays:F1} Tage";
    }

    private async Task AskUserAsync(InteractiveLinkingSession session, LinkingQuestionDTO question, CancellationToken ct)
    {
        session.CurrentQuestionId = question.QuestionId;
        session.CurrentQuestion = question.Message;
        session.CurrentOptions = question.Options;
        session.CurrentHints = question.Hints;
        session.OptionToHintIndex = question.OptionToHintIndex;

        await SendEventAsync(session, new LinkingEventDTO
        {
            EventType = "question",
            Message = question.Message,
            Transaction = question.Transaction,
            QuestionId = question.QuestionId,
            Options = question.Options,
            ProcessedCount = session.ProcessedCount,
            TotalCount = session.TotalCount
        });
    }

    /// <summary>
    /// Verarbeitet die User-Antwort und gibt ggf. eine neu gelernte Regel zurück
    /// </summary>
    private async Task<LearnedRuleDTO?> ProcessUserResponseAsync(
        CryptoTrackerDbContext dbContext,
        InteractiveLinkingSession session,
        UnlinkedTransactionDTO tx,
        List<LinkingHintDTO> hints,
        UserResponseDTO response,
        CancellationToken ct)
    {
        var responseText = response.Response;
        var responseTextLower = responseText.ToLowerInvariant();

        if (string.Equals(response.Action, VirtualWalletAction, StringComparison.OrdinalIgnoreCase))
        {
            var error = await LinkToVirtualWalletAsync(dbContext, session, tx, response, ct);
            if (!string.IsNullOrEmpty(error))
            {
                session.SkippedCount++;
                await SendEventAsync(session, new LinkingEventDTO
                {
                    EventType = "skipped",
                    Message = $"Übersprungen: {error}",
                    Transaction = tx,
                    ProcessedCount = session.ProcessedCount,
                    TotalCount = session.TotalCount
                });
            }
            return null;
        }

        // Option: Verknüpfen - prüfe ob die Response im OptionToHintIndex-Mapping ist
        if (session.OptionToHintIndex != null && session.OptionToHintIndex.TryGetValue(responseText, out var hintIndex))
        {
            if (hintIndex < hints.Count)
            {
                var hint = hints[hintIndex];
                var otherId = tx.IsSend ? hint.ReceiveId : hint.SendId;
                var otherTx = await GetTransactionDTOAsync(dbContext, otherId, ct);
                
                if (otherTx != null)
                {
                    await LinkTransactionsAsync(dbContext, session, tx, otherTx, 
                        $"Manuell verknüpft: {hint.Reason}", ct);
                    return null;
                }
            }
        }
        
        // Fallback: Verknüpfen (beginnt mit "→" oder "Verknüpfen")
        if ((responseText.StartsWith("→") || responseTextLower.StartsWith("verknüpfen")) && hints.Count > 0)
        {
            // Nimm den ersten Hint als Fallback
            var hint = hints[0];
            var otherId = tx.IsSend ? hint.ReceiveId : hint.SendId;
            var otherTx = await GetTransactionDTOAsync(dbContext, otherId, ct);
            
            if (otherTx != null)
            {
                await LinkTransactionsAsync(dbContext, session, tx, otherTx, 
                    $"Manuell verknüpft: {hint.Reason}", ct);
                return null;
            }
        }
        
        // Option: Externe Einnahme/Ausgabe
        if (responseTextLower.Contains("extern"))
        {
            var reason = "Vom Benutzer als extern markiert";
            LearnedRuleDTO? newRule = null;
            
            // Prüfe ob Regel gelernt werden soll
            if (response.ShouldRemember && !string.IsNullOrEmpty(tx.Comment))
            {
                newRule = new LearnedRuleDTO
                {
                    Id = Guid.NewGuid().ToString("N"),
                    RuleType = "comment_pattern",
                    Pattern = tx.Comment,
                    Action = "mark_external",
                    Description = $"Kommentar '{tx.Comment}' → Externe Einnahme",
                    CreatedAt = DateTimeOffset.UtcNow,
                    TimesApplied = 0
                };
                
                await SaveLearnedRuleAsync(dbContext, newRule, ct);

                reason = $"Extern (Regel gelernt: '{tx.Comment}')";

                await SendEventAsync(session, new LinkingEventDTO
                {
                    EventType = "rule_learned",
                    Message = $"Regel gelernt: Kommentar '{tx.Comment}' → Externe Einnahme"
                });
            }

            await MarkAsExternalAsync(dbContext, session, tx, reason, ct);
            return newRule;
        }
        
        // Option: Überspringen (default)
        session.SkippedCount++;
        await SendEventAsync(session, new LinkingEventDTO
        {
            EventType = "skipped",
            Message = $"Übersprungen: {tx.Symbol} {tx.Quantity:F8}",
            Transaction = tx,
            ProcessedCount = session.ProcessedCount,
            TotalCount = session.TotalCount
        });
        
        return null;
    }

    private async Task<string?> LinkToVirtualWalletAsync(
        CryptoTrackerDbContext dbContext,
        InteractiveLinkingSession session,
        UnlinkedTransactionDTO tx,
        UserResponseDTO response,
        CancellationToken ct)
    {
        var virtualWallet = await ResolveVirtualWalletAsync(dbContext, response, ct);
        if (virtualWallet == null)
        {
            return "Virtuelles Wallet konnte nicht gefunden oder erstellt werden.";
        }

        var oppositeType = tx.IsSend ? TransactionType.Receive : TransactionType.Send;
        var oppositeQuantity = tx.IsSend ? tx.QuantityAfterFee : tx.Quantity;

        if (oppositeQuantity <= 0)
        {
            return "Ungültige Menge für virtuelles Gegenstück.";
        }

        var oppositeTransaction = new CryptoTransaction
        {
            WalletId = virtualWallet.Id,
            DateTime = tx.DateTime,
            TransactionType = oppositeType,
            Symbol = tx.Symbol,
            Quantity = oppositeQuantity,
            Fee = 0m,
            Comment = $"Virtuelles Gegenstück zu #{tx.Id} ({tx.WalletName})",
            Address = tx.Address,
            Network = tx.Network,
            TransactionId = tx.TransactionId
        };

        dbContext.CryptoTransactions.Add(oppositeTransaction);
        await dbContext.SaveChangesAsync(ct);

        var oppositeDto = new UnlinkedTransactionDTO
        {
            Id = oppositeTransaction.Id,
            DateTime = oppositeTransaction.DateTime,
            Type = oppositeType.ToString(),
            Symbol = oppositeTransaction.Symbol,
            Quantity = oppositeTransaction.Quantity,
            QuantityAfterFee = oppositeTransaction.QuantityAfterFee,
            Comment = oppositeTransaction.Comment,
            Address = oppositeTransaction.Address,
            WalletName = virtualWallet.Name,
            TransactionId = oppositeTransaction.TransactionId,
            Network = oppositeTransaction.Network
        };

        await LinkTransactionsAsync(
            dbContext,
            session,
            tx,
            oppositeDto,
            $"Virtuelles Wallet: {virtualWallet.Name}",
            ct);

        return null;
    }

    private static async Task<Wallet?> ResolveVirtualWalletAsync(
        CryptoTrackerDbContext dbContext,
        UserResponseDTO response,
        CancellationToken ct)
    {
        if (response.VirtualWalletId.HasValue)
        {
            return await dbContext.Wallets
                .FirstOrDefaultAsync(w => w.Id == response.VirtualWalletId.Value && w.IsVirtual, ct);
        }

        var name = response.VirtualWalletName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var existing = await dbContext.Wallets
            .FirstOrDefaultAsync(w => w.Name == name, ct);

        if (existing != null)
        {
            return existing.IsVirtual ? existing : null;
        }

        var wallet = new Wallet
        {
            Name = name,
            IsVirtual = true
        };

        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync(ct);
        return wallet;
    }

    private async Task<UnlinkedTransactionDTO?> GetTransactionDTOAsync(
        CryptoTrackerDbContext dbContext,
        int id,
        CancellationToken ct)
    {
        return await dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Where(t => t.Id == id)
            .Select(t => new UnlinkedTransactionDTO
            {
                Id = t.Id,
                DateTime = t.DateTime,
                Type = t.TransactionType.ToString(),
                Symbol = t.Symbol,
                Quantity = t.Quantity,
                QuantityAfterFee = t.QuantityAfterFee,
                Comment = t.Comment,
                Address = t.Address,
                WalletName = t.Wallet.Name,
                TransactionId = t.TransactionId,
                Network = t.Network
            })
            .FirstOrDefaultAsync(ct);
    }

    private async Task<IList<LearnedRuleDTO>> LoadLearnedRulesAsync(
        CryptoTrackerDbContext dbContext,
        CancellationToken ct)
    {
        // Für jetzt: Lade aus AgentMemory
        var memory = await dbContext.AgentMemories
            .Where(m => m.AgentKey == "transaction-linking" && m.Key.StartsWith("rule:"))
            .ToListAsync(ct);

        return memory.Select(m => System.Text.Json.JsonSerializer.Deserialize<LearnedRuleDTO>(m.Value))
            .Where(r => r != null)
            .Cast<LearnedRuleDTO>()
            .ToList();
    }

    private async Task SaveLearnedRuleAsync(
        CryptoTrackerDbContext dbContext,
        LearnedRuleDTO rule,
        CancellationToken ct)
    {
        var memory = new AgentMemory
        {
            AgentKey = "transaction-linking",
            MemoryType = AgentMemoryType.UserDecision,
            Key = $"rule:{rule.Id}",
            Value = System.Text.Json.JsonSerializer.Serialize(rule),
            Description = rule.Description,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.AgentMemories.Add(memory);
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task<LinkingStatisticsDTO> GetStatisticsAsync(CryptoTrackerDbContext dbContext, CancellationToken ct)
    {
        var total = await dbContext.CryptoTransactions.CountAsync(ct);
        var linked = await dbContext.CryptoTransactions.CountAsync(t => t.OppositeTransactionId != null, ct);
        var external = await dbContext.CryptoTransactions.CountAsync(t => t.IsIntentionallyUnlinked, ct);
        var unlinked = await dbContext.CryptoTransactions.CountAsync(t => 
            t.OppositeTransactionId == null && !t.IsIntentionallyUnlinked, ct);

        return new LinkingStatisticsDTO
        {
            TotalTransactions = total,
            LinkedTransactions = linked,
            IntentionallyUnlinked = external,
            UnlinkedTransactions = unlinked
        };
    }

    private async Task SendEventAsync(InteractiveLinkingSession session, LinkingEventDTO evt)
    {
        await _hubContext.Clients.Group(session.SessionId)
            .SendAsync("OnLinkingEvent", evt);
    }

    private async Task SendSessionUpdateAsync(InteractiveLinkingSession session)
    {
        await _hubContext.Clients.Group(session.SessionId)
            .SendAsync("OnSessionUpdate", session.ToDTO());
    }
}

/// <summary>
/// Interne Session-Klasse
/// </summary>
internal class InteractiveLinkingSession
{
    public string SessionId { get; init; } = "";
    public DateTimeOffset StartedAt { get; init; }
    public bool IsActive { get; set; } = true;
    public int ProcessedCount { get; set; }
    public int TotalCount { get; set; }
    public int LinkedCount { get; set; }
    public int MarkedExternalCount { get; set; }
    public int SkippedCount { get; set; }
    public string? CurrentQuestionId { get; set; }
    public string? CurrentQuestion { get; set; }
    public UnlinkedTransactionDTO? CurrentTransaction { get; set; }
    public IList<string>? CurrentOptions { get; set; }
    public IList<LinkingHintDTO>? CurrentHints { get; set; }
    public Dictionary<string, int>? OptionToHintIndex { get; set; }
    public CancellationTokenSource CancellationSource { get; } = new();
    public required AsyncQueue<UserResponseDTO> UserResponseChannel { get; init; }

    public InteractiveLinkingSessionDTO ToDTO() => new()
    {
        SessionId = SessionId,
        IsActive = IsActive,
        ProcessedCount = ProcessedCount,
        TotalCount = TotalCount,
        LinkedCount = LinkedCount,
        MarkedExternalCount = MarkedExternalCount,
        SkippedCount = SkippedCount,
        CurrentQuestionId = CurrentQuestionId,
        CurrentQuestion = CurrentQuestion,
        CurrentTransaction = CurrentTransaction,
        CurrentOptions = CurrentOptions
    };
}

/// <summary>
/// DTO für Fragen an den User
/// </summary>
internal record LinkingQuestionDTO
{
    public string QuestionId { get; init; } = "";
    public string Message { get; init; } = "";
    public IList<string> Options { get; init; } = [];
    public UnlinkedTransactionDTO? Transaction { get; init; }
    public IList<LinkingHintDTO> Hints { get; init; } = [];
    public Dictionary<string, int> OptionToHintIndex { get; init; } = [];
}

/// <summary>
/// Einfache async Queue
/// </summary>
internal class AsyncQueue<T>
{
    private readonly System.Threading.Channels.Channel<T> _channel = 
        System.Threading.Channels.Channel.CreateUnbounded<T>();

    public async Task EnqueueAsync(T item)
    {
        await _channel.Writer.WriteAsync(item);
    }

    public async Task<T> DequeueAsync(CancellationToken ct = default)
    {
        return await _channel.Reader.ReadAsync(ct);
    }
}
