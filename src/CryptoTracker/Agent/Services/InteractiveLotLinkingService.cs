using System.Collections.Concurrent;
using CryptoTracker.Entities;
using CryptoTracker.Hubs;
using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Agent.Services;

/// <summary>
/// Service für interaktives Lot-Linking mit Human-in-the-Loop
/// </summary>
public class InteractiveLotLinkingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<LinkingHub> _hubContext;
    private readonly ILogger<InteractiveLotLinkingService> _logger;

    // Aktive Sessions
    private readonly ConcurrentDictionary<string, InteractiveLotLinkingSession> _sessions = new();

    public InteractiveLotLinkingService(
        IServiceScopeFactory scopeFactory,
        IHubContext<LinkingHub> hubContext,
        ILogger<InteractiveLotLinkingService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Startet eine neue interaktive Lot-Linking-Session
    /// </summary>
    public async Task<InteractiveLotLinkingSessionDTO> StartSessionAsync(CancellationToken ct = default)
    {
        var sessionId = $"lot-{Guid.NewGuid():N}";
        var session = new InteractiveLotLinkingSession
        {
            SessionId = sessionId,
            StartedAt = DateTimeOffset.UtcNow,
            UserResponseChannel = new AsyncQueue<LotLinkingUserResponseDTO>()
        };

        _sessions[sessionId] = session;

        // Lade initiale Statistiken
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CryptoTrackerDbContext>();
        
        var pendingCount = await GetPendingAssignmentCountAsync(dbContext, ct);
        session.TotalCount = pendingCount;

        // Starte den Linking-Prozess im Hintergrund
        _ = Task.Run(() => RunLotLinkingProcessAsync(session, ct), ct);

        return session.ToDTO();
    }

    /// <summary>
    /// User gibt Antwort auf Agent-Frage
    /// </summary>
    public async Task SubmitUserResponseAsync(string sessionId, LotLinkingUserResponseDTO response)
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
    public InteractiveLotLinkingSessionDTO? GetSessionStatus(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var session) ? session.ToDTO() : null;
    }

    /// <summary>
    /// Lädt gelernte Regeln für Lot-Linking
    /// </summary>
    public async Task<IList<LotLinkingRuleDTO>> GetLearnedRulesAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CryptoTrackerDbContext>();
        return await LoadLearnedRulesAsync(dbContext, ct);
    }

    /// <summary>
    /// Löscht eine gelernte Regel
    /// </summary>
    public async Task<bool> DeleteLearnedRuleAsync(string ruleId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CryptoTrackerDbContext>();
        
        var memory = await dbContext.AgentMemories
            .FirstOrDefaultAsync(m => m.AgentKey == "lot-linking" && m.Key == $"rule:{ruleId}", ct);
        
        if (memory != null)
        {
            dbContext.AgentMemories.Remove(memory);
            await dbContext.SaveChangesAsync(ct);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Hauptprozess für interaktives Lot-Linking
    /// </summary>
    private async Task RunLotLinkingProcessAsync(InteractiveLotLinkingSession session, CancellationToken ct)
    {
        var linkedCt = CancellationTokenSource.CreateLinkedTokenSource(ct, session.CancellationSource.Token);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CryptoTrackerDbContext>();
            var lotService = scope.ServiceProvider.GetRequiredService<LotService>();
            var coinRateService = scope.ServiceProvider.GetRequiredService<CoinRateService>();

            // 1. Lade alle ausstehenden Zuordnungen
            var pendingAssignments = await LoadPendingAssignmentsAsync(dbContext, linkedCt.Token);
            session.TotalCount = pendingAssignments.Count;

            await SendSessionUpdateAsync(session);

            // 2. Lade gelernte Regeln
            var activeRules = (await LoadLearnedRulesAsync(dbContext, linkedCt.Token)).ToList();

            // 3. Verarbeite jede ausstehende Zuordnung
            foreach (var assignment in pendingAssignments)
            {
                if (linkedCt.Token.IsCancellationRequested)
                    break;

                session.CurrentAssignment = assignment;
                session.ProcessedCount++;

                await SendEventAsync(session, new LotLinkingEventDTO
                {
                    EventType = "progress",
                    Message = $"Verarbeite {assignment.Symbol} {assignment.Quantity:F8} ({assignment.WalletName})",
                    Assignment = assignment,
                    ProcessedCount = session.ProcessedCount,
                    TotalCount = session.TotalCount
                });

                // Prüfe ob gelernte Regel zutrifft
                var matchingRule = FindMatchingRule(assignment, activeRules);
                if (matchingRule != null)
                {
                    var newRule = await ApplyRuleAsync(dbContext, lotService, coinRateService, session, assignment, matchingRule, linkedCt.Token);
                    if (newRule != null)
                    {
                        activeRules.Add(newRule);
                    }
                    continue;
                }

                // Entscheide basierend auf Typ
                if (assignment.Type == "Transaction" && assignment.Direction == "Receive")
                {
                    await HandleReceiveTransactionAsync(dbContext, lotService, coinRateService, session, assignment, activeRules, linkedCt.Token);
                }
                else if (assignment.Type == "Trade" && assignment.Direction == "Sell")
                {
                    await HandleSellTradeAsync(dbContext, lotService, session, assignment, activeRules, linkedCt.Token);
                }
            }

            // 4. Session abschließen
            session.IsActive = false;
            var finalStats = await GetStatisticsAsync(dbContext, linkedCt.Token);

            await _hubContext.Clients.Group(session.SessionId)
                .SendAsync("OnLotLinkingSessionCompleted", finalStats, linkedCt.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Lot-Linking Session {SessionId} wurde abgebrochen", session.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler in Lot-Linking Session {SessionId}", session.SessionId);
            await _hubContext.Clients.Group(session.SessionId)
                .SendAsync("OnLotLinkingError", ex.Message, CancellationToken.None);
        }
        finally
        {
            _sessions.TryRemove(session.SessionId, out _);
        }
    }

    /// <summary>
    /// Behandelt eine Receive-Transaktion ohne verknüpfte Send-Transaktion
    /// </summary>
    private async Task HandleReceiveTransactionAsync(
        CryptoTrackerDbContext dbContext,
        LotService lotService,
        CoinRateService coinRateService,
        InteractiveLotLinkingSession session,
        PendingLotAssignmentDTO assignment,
        List<LotLinkingRuleDTO> activeRules,
        CancellationToken ct)
    {
        // Lade Transaktion für Details
        var transaction = await dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.Id == assignment.Id, ct);

        if (transaction == null) return;

        // Prüfe ob es eine verknüpfte Send-Transaktion gibt (interner Transfer)
        if (transaction.OppositeTransactionId.HasValue)
        {
            // Interner Transfer - Lots sollten vom Send-Wallet übertragen werden
            await HandleInternalTransferAsync(dbContext, lotService, session, transaction, ct);
            return;
        }

        // Prüfe ob Kommentar eindeutige Keywords enthält (Auto-Erkennung)
        var autoDetectedType = DetectAcquisitionTypeFromComment(transaction.Comment);
        if (autoDetectedType.HasValue)
        {
            _logger.LogInformation("Auto-erkannt aus Kommentar '{Comment}': {Type}", 
                transaction.Comment, autoDetectedType.Value);
            
            await CreateLotFromAutoDetectionAsync(dbContext, lotService, coinRateService, session, 
                assignment, transaction, autoDetectedType.Value, ct);
            return;
        }

        // Externes Receive - frage User nach Herkunft
        var question = BuildExternalReceiveQuestion(assignment, transaction.Comment);
        await AskUserAsync(session, question, ct);

        // Warte auf Antwort
        var response = await session.UserResponseChannel.DequeueAsync(ct);
        var newRule = await ProcessReceiveResponseAsync(dbContext, lotService, coinRateService, session, assignment, transaction, response, activeRules, ct);
        
        if (newRule != null)
        {
            activeRules.Add(newRule);
        }
    }

    /// <summary>
    /// Erkennt den Acquisition-Type automatisch aus dem Kommentar anhand von Keywords
    /// </summary>
    private static LotAcquisitionType? DetectAcquisitionTypeFromComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return null;

        var lowerComment = comment.ToLowerInvariant();

        // Staking Keywords (häufigste zuerst)
        if (lowerComment.Contains("staking") || 
            lowerComment.Contains("stake reward") ||
            lowerComment.Contains("pos reward") ||
            lowerComment.Contains("delegation reward") ||
            lowerComment.Contains("validator reward"))
        {
            return LotAcquisitionType.Staking;
        }

        // Airdrop Keywords
        if (lowerComment.Contains("airdrop") || 
            lowerComment.Contains("air drop") ||
            lowerComment.Contains("空投")) // Chinesisch für Airdrop
        {
            return LotAcquisitionType.Airdrop;
        }

        // Mining Keywords
        if (lowerComment.Contains("mining") || 
            lowerComment.Contains("miner reward") ||
            lowerComment.Contains("block reward") ||
            lowerComment.Contains("pow reward"))
        {
            return LotAcquisitionType.Mining;
        }

        return null;
    }

    /// <summary>
    /// Erstellt ein Lot basierend auf Auto-Erkennung aus dem Kommentar
    /// </summary>
    private async Task CreateLotFromAutoDetectionAsync(
        CryptoTrackerDbContext dbContext,
        LotService lotService,
        CoinRateService coinRateService,
        InteractiveLotLinkingSession session,
        PendingLotAssignmentDTO assignment,
        CryptoTransaction transaction,
        LotAcquisitionType acquisitionType,
        CancellationToken ct)
    {
        // Hole EUR-Preis zum Zeitpunkt
        var (rate, _) = await coinRateService.GetPreviousCloseRateWithSourceAsync(
            assignment.Symbol,
            transaction.DateTime.UtcDateTime);
        var acquisitionPriceEur = rate ?? 0;

        // Erstelle Lot
        var lotRequest = new ManualLotRequest
        {
            Symbol = assignment.Symbol,
            WalletId = assignment.WalletId,
            Quantity = assignment.Quantity,
            AcquisitionDate = transaction.DateTime,
            AcquisitionPriceEur = acquisitionPriceEur,
            AcquisitionType = acquisitionType,
            SourceTransactionId = transaction.Id,
            Note = $"Auto-erkannt aus Kommentar: {acquisitionType}"
        };

        var lot = await lotService.CreateManualLotAsync(lotRequest);
        session.CreatedLotsCount++;

        await SendEventAsync(session, new LotLinkingEventDTO
        {
            EventType = "lot_created",
            Message = $"Lot erstellt (Auto): {assignment.Symbol} {assignment.Quantity:F8} @ {acquisitionPriceEur:F2}€ ({acquisitionType}) - Kommentar enthielt '{acquisitionType}'",
            Assignment = assignment,
            CreatedLot = ToLotDTO(lot),
            ProcessedCount = session.ProcessedCount,
            TotalCount = session.TotalCount
        });
    }

    /// <summary>
    /// Behandelt einen internen Transfer (Send → Receive verknüpft)
    /// </summary>
    private async Task HandleInternalTransferAsync(
        CryptoTrackerDbContext dbContext,
        LotService lotService,
        InteractiveLotLinkingSession session,
        CryptoTransaction receiveTransaction,
        CancellationToken ct)
    {
        var sendTransaction = await dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.Id == receiveTransaction.OppositeTransactionId, ct);

        if (sendTransaction == null)
        {
            await SendEventAsync(session, new LotLinkingEventDTO
            {
                EventType = "error",
                Message = $"Send-Transaktion #{receiveTransaction.OppositeTransactionId} nicht gefunden"
            });
            return;
        }

        // Hole verfügbare Lots vom Send-Wallet
        var availableLots = await lotService.GetAvailableLotsAsync(
            sendTransaction.WalletId, 
            sendTransaction.Symbol);

        if (availableLots.Count == 0)
        {
            // Keine Lots verfügbar - frage User
            var question = new LotLinkingQuestionDTO
            {
                QuestionId = Guid.NewGuid().ToString("N"),
                Message = $"Keine Lots auf {sendTransaction.Wallet.Name} für Transfer von {sendTransaction.Quantity:F8} {sendTransaction.Symbol} verfügbar.",
                Options = ["Überspringen", "Abbrechen"],
                Assignment = ToPendingDTO(receiveTransaction)
            };
            
            await AskUserAsync(session, question, ct);
            var resp = await session.UserResponseChannel.DequeueAsync(ct);
            
            if (resp.Response.Contains("Abbrechen", StringComparison.OrdinalIgnoreCase))
            {
                session.CancellationSource.Cancel();
            }
            else
            {
                session.SkippedCount++;
            }
            return;
        }

        // FIFO-Vorschlag erstellen
        var fifoAllocations = await lotService.SuggestFifoAllocationAsync(
            sendTransaction.WalletId,
            sendTransaction.Symbol,
            sendTransaction.QuantityAfterFee);

        if (fifoAllocations.Sum(a => a.Quantity) >= sendTransaction.QuantityAfterFee * 0.999m) // 0.1% Toleranz
        {
            // Automatisch zuordnen
            var allocations = fifoAllocations.Select(a => new LotAllocation 
            { 
                LotId = a.LotId, 
                Quantity = a.Quantity 
            }).ToList();

            var resultingLots = await lotService.TransferLotsAsync(
                sendTransaction.Id,
                receiveTransaction.Id,
                allocations);

            session.AssignedCount++;

            await SendEventAsync(session, new LotLinkingEventDTO
            {
                EventType = "assigned",
                Message = $"Transfer: {sendTransaction.Symbol} {sendTransaction.QuantityAfterFee:F8} ({sendTransaction.Wallet.Name} → {receiveTransaction.Wallet.Name})",
                Assignment = ToPendingDTO(receiveTransaction),
                ProcessedCount = session.ProcessedCount,
                TotalCount = session.TotalCount
            });
        }
        else
        {
            // Nicht genug Lots - frage User
            session.SkippedCount++;
            await SendEventAsync(session, new LotLinkingEventDTO
            {
                EventType = "skipped",
                Message = $"Nicht genug Lots für Transfer: {sendTransaction.Symbol} (benötigt: {sendTransaction.QuantityAfterFee:F8}, verfügbar: {fifoAllocations.Sum(a => a.Quantity):F8})",
                ProcessedCount = session.ProcessedCount,
                TotalCount = session.TotalCount
            });
        }
    }

    /// <summary>
    /// Behandelt einen Verkauf (Trade mit Typ Sell)
    /// </summary>
    private async Task HandleSellTradeAsync(
        CryptoTrackerDbContext dbContext,
        LotService lotService,
        InteractiveLotLinkingSession session,
        PendingLotAssignmentDTO assignment,
        List<LotLinkingRuleDTO> activeRules,
        CancellationToken ct)
    {
        var trade = await dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.Id == assignment.Id, ct);

        if (trade == null) return;

        // Hole verfügbare Lots
        var availableLots = await lotService.GetAvailableLotsAsync(trade.WalletId, trade.Symbol);

        if (availableLots.Count == 0)
        {
            session.SkippedCount++;
            await SendEventAsync(session, new LotLinkingEventDTO
            {
                EventType = "skipped",
                Message = $"Keine Lots für Verkauf: {trade.Symbol} {trade.Quantity:F8}",
                Assignment = assignment,
                ProcessedCount = session.ProcessedCount,
                TotalCount = session.TotalCount
            });
            return;
        }

        // FIFO-Vorschlag
        var fifoAllocations = await lotService.SuggestFifoAllocationAsync(
            trade.WalletId,
            trade.Symbol,
            trade.Quantity,
            prioritizeAltbestand: true); // Altbestand zuerst (steuerfrei)

        if (fifoAllocations.Sum(a => a.Quantity) >= trade.Quantity * 0.999m)
        {
            // Automatisch zuordnen mit FIFO
            var allocations = fifoAllocations.Select(a => new LotAllocation 
            { 
                LotId = a.LotId, 
                Quantity = a.Quantity 
            }).ToList();

            // Berechne Verkaufspreis in EUR
            var salePriceEur = trade.Price; // Annahme: bereits in EUR oder konvertiert
            if (!trade.OppositeSymbol.Equals("EUR", StringComparison.OrdinalIgnoreCase))
            {
                // Müsste konvertiert werden - für jetzt überspringen
                session.SkippedCount++;
                await SendEventAsync(session, new LotLinkingEventDTO
                {
                    EventType = "skipped",
                    Message = $"Verkauf in {trade.OppositeSymbol} - manuelle Zuordnung erforderlich",
                    Assignment = assignment
                });
                return;
            }

            var result = await lotService.SellLotsAsync(trade.Id, allocations, salePriceEur);

            session.AssignedCount++;

            var taxInfo = result.TaxFreeQuantity > 0 
                ? $" (Steuerfrei: {result.TaxFreeGain:F2}€, Steuerpflichtig: {result.TaxableGain:F2}€)"
                : $" (Gewinn: {result.TotalRealizedGain:F2}€)";

            await SendEventAsync(session, new LotLinkingEventDTO
            {
                EventType = "assigned",
                Message = $"Verkauf: {trade.Symbol} {trade.Quantity:F8} für {trade.Price * trade.Quantity:F2}€{taxInfo}",
                Assignment = assignment,
                ProcessedCount = session.ProcessedCount,
                TotalCount = session.TotalCount
            });
        }
        else
        {
            // Nicht genug Lots
            session.SkippedCount++;
            await SendEventAsync(session, new LotLinkingEventDTO
            {
                EventType = "skipped",
                Message = $"Nicht genug Lots für Verkauf: {trade.Symbol} (benötigt: {trade.Quantity:F8})",
                Assignment = assignment,
                ProcessedCount = session.ProcessedCount,
                TotalCount = session.TotalCount
            });
        }
    }

    /// <summary>
    /// Baut Frage für externes Receive
    /// </summary>
    private LotLinkingQuestionDTO BuildExternalReceiveQuestion(PendingLotAssignmentDTO assignment, string? comment)
    {
        var options = new List<string>
        {
            "Externer Eingang (Kauf woanders)",
            "Airdrop",
            "Staking Reward",
            "Mining Reward",
            "Schenkung / Erbe",
            "Überspringen"
        };

        var message = $"**Externes Receive:** {assignment.Symbol} {assignment.Quantity:F8}\n" +
                      $"**Wallet:** {assignment.WalletName}\n" +
                      $"**Datum:** {assignment.DateTime:dd.MM.yyyy HH:mm}";

        if (!string.IsNullOrEmpty(comment))
        {
            message += $"\n**Kommentar:** \"{comment}\"";
        }

        message += "\n\n**Woher stammen diese Coins?**";

        return new LotLinkingQuestionDTO
        {
            QuestionId = Guid.NewGuid().ToString("N"),
            Message = message,
            Options = options,
            Assignment = assignment,
            RequiresPrice = true
        };
    }

    /// <summary>
    /// Verarbeitet die User-Antwort für externes Receive
    /// </summary>
    private async Task<LotLinkingRuleDTO?> ProcessReceiveResponseAsync(
        CryptoTrackerDbContext dbContext,
        LotService lotService,
        CoinRateService coinRateService,
        InteractiveLotLinkingSession session,
        PendingLotAssignmentDTO assignment,
        CryptoTransaction transaction,
        LotLinkingUserResponseDTO response,
        List<LotLinkingRuleDTO> activeRules,
        CancellationToken ct)
    {
        var responseText = response.Response.ToLowerInvariant();
        LotLinkingRuleDTO? newRule = null;

        if (responseText.Contains("überspringen") || responseText.Contains("skip"))
        {
            session.SkippedCount++;
            await SendEventAsync(session, new LotLinkingEventDTO
            {
                EventType = "skipped",
                Message = $"Übersprungen: {assignment.Symbol} {assignment.Quantity:F8}",
                Assignment = assignment,
                ProcessedCount = session.ProcessedCount,
                TotalCount = session.TotalCount
            });
            return null;
        }

        // Bestimme Acquisition Type
        var acquisitionType = responseText switch
        {
            var r when r.Contains("airdrop") => LotAcquisitionType.Airdrop,
            var r when r.Contains("staking") => LotAcquisitionType.Staking,
            var r when r.Contains("mining") => LotAcquisitionType.Mining,
            var r when r.Contains("schenkung") || r.Contains("erbe") => LotAcquisitionType.Gift,
            _ => LotAcquisitionType.ExternalDeposit
        };

        // Hole EUR-Preis zum Zeitpunkt
        decimal acquisitionPriceEur;
        if (response.AcquisitionPriceEur.HasValue && response.AcquisitionPriceEur.Value > 0)
        {
            acquisitionPriceEur = response.AcquisitionPriceEur.Value;
        }
        else
        {
            // Versuche Marktpreis zu holen
            var (rate, _) = await coinRateService.GetPreviousCloseRateWithSourceAsync(
                assignment.Symbol,
                transaction.DateTime.UtcDateTime);
            acquisitionPriceEur = rate ?? 0;
        }

        // Erstelle Lot
        var lotRequest = new ManualLotRequest
        {
            Symbol = assignment.Symbol,
            WalletId = assignment.WalletId,
            Quantity = assignment.Quantity,
            AcquisitionDate = transaction.DateTime,
            AcquisitionPriceEur = acquisitionPriceEur,
            AcquisitionType = acquisitionType,
            SourceTransactionId = transaction.Id,
            Note = response.Note ?? $"Erstellt via Lot-Linking Wizard ({acquisitionType})"
        };

        var lot = await lotService.CreateManualLotAsync(lotRequest);
        session.CreatedLotsCount++;

        await SendEventAsync(session, new LotLinkingEventDTO
        {
            EventType = "lot_created",
            Message = $"Lot erstellt: {assignment.Symbol} {assignment.Quantity:F8} @ {acquisitionPriceEur:F2}€ ({acquisitionType})",
            Assignment = assignment,
            CreatedLot = ToLotDTO(lot),
            ProcessedCount = session.ProcessedCount,
            TotalCount = session.TotalCount
        });

        // Regel lernen wenn gewünscht
        if (response.ShouldRemember && !string.IsNullOrEmpty(transaction.Comment))
        {
            var action = acquisitionType switch
            {
                LotAcquisitionType.Airdrop => "create_lot_airdrop",
                LotAcquisitionType.Staking => "create_lot_staking",
                LotAcquisitionType.Mining => "create_lot_mining",
                _ => "create_lot_external"
            };

            // Extrahiere ein sinnvolles Pattern aus dem Kommentar
            var pattern = ExtractMeaningfulPattern(transaction.Comment);

            newRule = new LotLinkingRuleDTO
            {
                Id = Guid.NewGuid().ToString("N"),
                RuleType = "comment_pattern",
                Pattern = pattern,
                Action = action,
                Description = $"Kommentar enthält '{pattern}' → {acquisitionType}",
                CreatedAt = DateTimeOffset.UtcNow,
                TimesApplied = 0
            };

            await SaveLearnedRuleAsync(dbContext, newRule, ct);

            await SendEventAsync(session, new LotLinkingEventDTO
            {
                EventType = "rule_learned",
                Message = $"Regel gelernt: Kommentar enthält '{pattern}' → {acquisitionType}"
            });
        }

        return newRule;
    }

    /// <summary>
    /// Findet passende Regel für comment_pattern, symbol_pattern und wallet_pattern
    /// </summary>
    private LotLinkingRuleDTO? FindMatchingRule(PendingLotAssignmentDTO assignment, IList<LotLinkingRuleDTO> rules)
    {
        foreach (var rule in rules)
        {
            var matches = rule.RuleType switch
            {
                "comment_pattern" => !string.IsNullOrEmpty(assignment.Comment) &&
                                     assignment.Comment.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase),
                "symbol_pattern" => assignment.Symbol.Equals(rule.Pattern, StringComparison.OrdinalIgnoreCase),
                "wallet_pattern" => assignment.WalletName.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase),
                _ => false
            };

            if (matches)
            {
                _logger.LogInformation("Regel gefunden: {RuleType} '{Pattern}' → {Action} für {Symbol}",
                    rule.RuleType, rule.Pattern, rule.Action, assignment.Symbol);
                return rule;
            }
        }
        return null;
    }

    /// <summary>
    /// Wendet eine Regel an
    /// </summary>
    private async Task<LotLinkingRuleDTO?> ApplyRuleAsync(
        CryptoTrackerDbContext dbContext,
        LotService lotService,
        CoinRateService coinRateService,
        InteractiveLotLinkingSession session,
        PendingLotAssignmentDTO assignment,
        LotLinkingRuleDTO rule,
        CancellationToken ct)
    {
        _logger.LogInformation("Wende Regel an: {Pattern} → {Action}", rule.Pattern, rule.Action);

        if (rule.Action.StartsWith("create_lot_"))
        {
            var transaction = await dbContext.CryptoTransactions.FindAsync([assignment.Id], ct);
            if (transaction == null) return null;

            var acquisitionType = rule.Action switch
            {
                "create_lot_airdrop" => LotAcquisitionType.Airdrop,
                "create_lot_staking" => LotAcquisitionType.Staking,
                "create_lot_mining" => LotAcquisitionType.Mining,
                _ => LotAcquisitionType.ExternalDeposit
            };

            var (rate, _) = await coinRateService.GetPreviousCloseRateWithSourceAsync(
                assignment.Symbol,
                transaction.DateTime.UtcDateTime);

            var lotRequest = new ManualLotRequest
            {
                Symbol = assignment.Symbol,
                WalletId = assignment.WalletId,
                Quantity = assignment.Quantity,
                AcquisitionDate = transaction.DateTime,
                AcquisitionPriceEur = rate ?? 0,
                AcquisitionType = acquisitionType,
                SourceTransactionId = transaction.Id,
                Note = $"Auto-erstellt via Regel: {rule.Description}"
            };

            var lot = await lotService.CreateManualLotAsync(lotRequest);
            session.CreatedLotsCount++;

            await SendEventAsync(session, new LotLinkingEventDTO
            {
                EventType = "lot_created",
                Message = $"Lot erstellt (Regel): {assignment.Symbol} {assignment.Quantity:F8} ({acquisitionType})",
                Assignment = assignment,
                CreatedLot = ToLotDTO(lot),
                ProcessedCount = session.ProcessedCount,
                TotalCount = session.TotalCount
            });

            // Update usage count
            await UpdateRuleUsageAsync(dbContext, rule.Id, ct);
        }
        else if (rule.Action == "skip")
        {
            session.SkippedCount++;
            await SendEventAsync(session, new LotLinkingEventDTO
            {
                EventType = "skipped",
                Message = $"Übersprungen (Regel): {assignment.Symbol} {assignment.Quantity:F8}",
                Assignment = assignment,
                ProcessedCount = session.ProcessedCount,
                TotalCount = session.TotalCount
            });
        }

        return null;
    }

    private async Task AskUserAsync(InteractiveLotLinkingSession session, LotLinkingQuestionDTO question, CancellationToken ct)
    {
        session.CurrentQuestionId = question.QuestionId;
        session.CurrentQuestion = question.Message;
        session.CurrentOptions = question.Options;

        await SendEventAsync(session, new LotLinkingEventDTO
        {
            EventType = "question",
            Message = question.Message,
            Assignment = question.Assignment,
            QuestionId = question.QuestionId,
            Options = question.Options,
            LotOptions = question.LotOptions,
            ProcessedCount = session.ProcessedCount,
            TotalCount = session.TotalCount
        });
    }

    /// <summary>
    /// Extrahiert ein sinnvolles Pattern aus einem Kommentar.
    /// Entfernt dynamische Teile wie IDs, Hashes, Timestamps.
    /// </summary>
    private static string ExtractMeaningfulPattern(string comment)
    {
        // Bekannte Keywords die wir als Pattern verwenden können
        var keywords = new[]
        {
            "staking", "stake", "delegation", "validator", "pos reward",
            "airdrop", "air drop", "空投",
            "mining", "miner", "block reward", "pow reward",
            "distribution", "reward", "bonus", "referral",
            "transfer", "deposit", "withdrawal"
        };

        var lowerComment = comment.ToLowerInvariant();
        
        // Suche nach bekannten Keywords
        foreach (var keyword in keywords)
        {
            if (lowerComment.Contains(keyword))
            {
                return keyword;
            }
        }

        // Fallback: Entferne dynamische Teile (IDs, Hashes, Zahlen am Ende)
        // z.B. "Distribution - staking project send. detailId-92954850" → "Distribution - staking project send"
        var pattern = comment;
        
        // Entferne detailId-XXXXX, id-XXXXX, hash-XXXXX etc.
        pattern = System.Text.RegularExpressions.Regex.Replace(
            pattern, 
            @"\s*(detailId|id|hash|txid|ref|reference)[-_]?\d+[a-fA-F0-9]*", 
            "", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        // Entferne lange Hex-Strings (Transaction Hashes)
        pattern = System.Text.RegularExpressions.Regex.Replace(
            pattern, 
            @"[a-fA-F0-9]{20,}", 
            "");
        
        // Entferne Timestamps und Datumsangaben
        pattern = System.Text.RegularExpressions.Regex.Replace(
            pattern, 
            @"\d{4}[-/]\d{2}[-/]\d{2}|\d{2}[-/]\d{2}[-/]\d{4}|\d{10,}", 
            "");
        
        // Bereinige Leerzeichen und Satzzeichen am Ende
        pattern = pattern.Trim().TrimEnd('.', ',', '-', '_', ' ');
        
        // Wenn das Pattern zu kurz wird, nimm die ersten sinnvollen Wörter
        if (pattern.Length < 5)
        {
            var words = comment.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
            pattern = string.Join(" ", words.Take(3));
        }

        return pattern;
    }

    #region Helper Methods

    private async Task<int> GetPendingAssignmentCountAsync(CryptoTrackerDbContext dbContext, CancellationToken ct)
    {
        var receiveCount = await dbContext.CryptoTransactions
            .CountAsync(t => t.TransactionType == TransactionType.Receive
                          && !t.LotAssignmentConfirmed, ct);

        var sellCount = await dbContext.CryptoTrades
            .CountAsync(t => t.TradeType == TradeType.Sell
                          && !t.LotAssignmentConfirmed, ct);

        return receiveCount + sellCount;
    }

    private async Task<IList<PendingLotAssignmentDTO>> LoadPendingAssignmentsAsync(
        CryptoTrackerDbContext dbContext,
        CancellationToken ct)
    {
        var transactions = await dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Include(t => t.OppositeWallet)
            .Where(t => t.TransactionType == TransactionType.Receive
                     && !t.LotAssignmentConfirmed)
            .OrderBy(t => t.DateTime)
            .Select(t => new PendingLotAssignmentDTO(
                "Transaction",
                t.Id,
                t.DateTime,
                t.Symbol,
                t.Quantity,
                t.Wallet.Name,
                t.WalletId,
                "Receive",
                t.OppositeWallet != null ? t.OppositeWallet.Name : null,
                t.Comment))
            .ToListAsync(ct);

        var trades = await dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .Where(t => t.TradeType == TradeType.Sell
                     && !t.LotAssignmentConfirmed)
            .OrderBy(t => t.DateTime)
            .Select(t => new PendingLotAssignmentDTO(
                "Trade",
                t.Id,
                t.DateTime,
                t.Symbol,
                t.Quantity,
                t.Wallet.Name,
                t.WalletId,
                "Sell",
                null,
                t.Comment))
            .ToListAsync(ct);

        return transactions.Concat(trades)
            .OrderBy(a => a.DateTime)
            .ToList();
    }

    private async Task<IList<LotLinkingRuleDTO>> LoadLearnedRulesAsync(
        CryptoTrackerDbContext dbContext,
        CancellationToken ct)
    {
        var memory = await dbContext.AgentMemories
            .Where(m => m.AgentKey == "lot-linking" && m.Key.StartsWith("rule:"))
            .ToListAsync(ct);

        return memory
            .Select(m => System.Text.Json.JsonSerializer.Deserialize<LotLinkingRuleDTO>(m.Value))
            .Where(r => r != null)
            .Cast<LotLinkingRuleDTO>()
            .ToList();
    }

    private async Task SaveLearnedRuleAsync(
        CryptoTrackerDbContext dbContext,
        LotLinkingRuleDTO rule,
        CancellationToken ct)
    {
        var memory = new AgentMemory
        {
            AgentKey = "lot-linking",
            MemoryType = AgentMemoryType.UserDecision,
            Key = $"rule:{rule.Id}",
            Value = System.Text.Json.JsonSerializer.Serialize(rule),
            Description = rule.Description,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.AgentMemories.Add(memory);
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task UpdateRuleUsageAsync(
        CryptoTrackerDbContext dbContext,
        string ruleId,
        CancellationToken ct)
    {
        var memory = await dbContext.AgentMemories
            .FirstOrDefaultAsync(m => m.AgentKey == "lot-linking" && m.Key == $"rule:{ruleId}", ct);

        if (memory != null)
        {
            var rule = System.Text.Json.JsonSerializer.Deserialize<LotLinkingRuleDTO>(memory.Value);
            if (rule != null)
            {
                var updated = rule with { TimesApplied = rule.TimesApplied + 1 };
                memory.Value = System.Text.Json.JsonSerializer.Serialize(updated);
                await dbContext.SaveChangesAsync(ct);
            }
        }
    }

    private async Task<LotLinkingStatisticsDTO> GetStatisticsAsync(
        CryptoTrackerDbContext dbContext,
        CancellationToken ct)
    {
        var pendingReceive = await dbContext.CryptoTransactions
            .CountAsync(t => t.TransactionType == TransactionType.Receive && !t.LotAssignmentConfirmed, ct);
        var pendingSell = await dbContext.CryptoTrades
            .CountAsync(t => t.TradeType == TradeType.Sell && !t.LotAssignmentConfirmed, ct);
        var completedTx = await dbContext.CryptoTransactions
            .CountAsync(t => t.LotAssignmentConfirmed, ct);
        var totalLots = await dbContext.AssetLots.CountAsync(ct);

        return new LotLinkingStatisticsDTO
        {
            TotalPendingAssignments = pendingReceive + pendingSell,
            PendingReceiveTransactions = pendingReceive,
            PendingSellTrades = pendingSell,
            CompletedAssignments = completedTx,
            LotsCreated = totalLots
        };
    }

    private async Task SendEventAsync(InteractiveLotLinkingSession session, LotLinkingEventDTO evt)
    {
        await _hubContext.Clients.Group(session.SessionId)
            .SendAsync("OnLotLinkingEvent", evt);
    }

    private async Task SendSessionUpdateAsync(InteractiveLotLinkingSession session)
    {
        await _hubContext.Clients.Group(session.SessionId)
            .SendAsync("OnLotLinkingSessionUpdate", session.ToDTO());
    }

    private static PendingLotAssignmentDTO ToPendingDTO(CryptoTransaction t) => new(
        "Transaction",
        t.Id,
        t.DateTime,
        t.Symbol,
        t.Quantity,
        t.Wallet?.Name ?? "",
        t.WalletId,
        t.TransactionType.ToString(),
        t.OppositeWallet?.Name,
        t.Comment);

    private static LotDTO ToLotDTO(AssetLot lot) => new(
        lot.Id,
        lot.Symbol,
        lot.CurrentWalletId,
        lot.CurrentWallet?.Name ?? "",
        lot.RemainingQuantity,
        lot.OriginalQuantity,
        lot.AcquisitionDate,
        lot.AcquisitionPriceEur,
        lot.TotalAcquisitionCostEur,
        lot.AcquisitionType.ToString(),
        lot.IsAltbestand,
        lot.IsFullyConsumed,
        lot.Note,
        lot.ParentLotId,
        lot.SourceTradeId,
        lot.SourceTransactionId,
        lot.IsFlowComplete,
        lot.FlowIncompleteReason);

    #endregion
}

/// <summary>
/// Interne Session-Klasse für Lot-Linking
/// </summary>
internal class InteractiveLotLinkingSession
{
    public string SessionId { get; init; } = "";
    public DateTimeOffset StartedAt { get; init; }
    public bool IsActive { get; set; } = true;
    public int ProcessedCount { get; set; }
    public int TotalCount { get; set; }
    public int AssignedCount { get; set; }
    public int CreatedLotsCount { get; set; }
    public int SkippedCount { get; set; }
    public string? CurrentQuestionId { get; set; }
    public string? CurrentQuestion { get; set; }
    public PendingLotAssignmentDTO? CurrentAssignment { get; set; }
    public IList<string>? CurrentOptions { get; set; }
    public CancellationTokenSource CancellationSource { get; } = new();
    public required AsyncQueue<LotLinkingUserResponseDTO> UserResponseChannel { get; init; }

    public InteractiveLotLinkingSessionDTO ToDTO() => new()
    {
        SessionId = SessionId,
        IsActive = IsActive,
        ProcessedCount = ProcessedCount,
        TotalCount = TotalCount,
        AssignedCount = AssignedCount,
        CreatedLotsCount = CreatedLotsCount,
        SkippedCount = SkippedCount,
        CurrentQuestionId = CurrentQuestionId,
        CurrentQuestion = CurrentQuestion,
        CurrentAssignment = CurrentAssignment,
        CurrentOptions = CurrentOptions
    };
}

/// <summary>
/// DTO für Fragen an den User
/// </summary>
internal record LotLinkingQuestionDTO
{
    public string QuestionId { get; init; } = "";
    public string Message { get; init; } = "";
    public IList<string> Options { get; init; } = [];
    public IList<LotOptionDTO>? LotOptions { get; init; }
    public PendingLotAssignmentDTO? Assignment { get; init; }
    public bool RequiresPrice { get; init; }
}
