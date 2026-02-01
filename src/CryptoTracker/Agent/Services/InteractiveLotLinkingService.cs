using System.Collections.Concurrent;
using System.Text;
using CryptoTracker.Agent.Common;
using CryptoTracker.Agent.Definitions;
using CryptoTracker.Entities;
using CryptoTracker.Hubs;
using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Agent.Services;

/// <summary>
/// Service für interaktives Lot-Linking mit Human-in-the-Loop (agentisch)
/// </summary>
public class InteractiveLotLinkingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<LinkingHub> _hubContext;
    private readonly ILogger<InteractiveLotLinkingService> _logger;
    private readonly ILotLinkingAgentContextAccessor _contextAccessor;

    // Aktive Sessions
    private readonly ConcurrentDictionary<string, InteractiveLotLinkingSession> _sessions = new();

    public InteractiveLotLinkingService(
        IServiceScopeFactory scopeFactory,
        IHubContext<LinkingHub> hubContext,
        ILogger<InteractiveLotLinkingService> logger,
        ILotLinkingAgentContextAccessor contextAccessor)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
        _contextAccessor = contextAccessor;
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
    /// Hauptprozess für interaktives Lot-Linking (agentisch, iterativ)
    /// </summary>
    private async Task RunLotLinkingProcessAsync(InteractiveLotLinkingSession session, CancellationToken ct)
    {
        var linkedCt = CancellationTokenSource.CreateLinkedTokenSource(ct, session.CancellationSource.Token);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CryptoTrackerDbContext>();
            var agentBuilder = scope.ServiceProvider.GetRequiredService<AILinkingAgentBuilder>();

            if (!agentBuilder.IsConfigured())
            {
                await SendEventAsync(session, new LotLinkingEventDTO
                {
                    EventType = "error",
                    Message = "AI-Service nicht konfiguriert. Bitte OpenAI-Einstellungen prüfen.",
                    ProcessedCount = 0,
                    TotalCount = 0
                });
                return;
            }

            session.TotalCount = await GetPendingAssignmentCountAsync(dbContext, linkedCt.Token);
            await SendSessionUpdateAsync(session);

            var agent = agentBuilder.BuildAgent(LotLinkingAgentDefinition.KEY);

            session.History.Add(new ChatMessageDTO
            {
                Role = "user",
                Content = "Starte eine neue Lot-Linking-Session. " +
                          "Lade gespeicherte Regeln und alle ausstehenden Zuordnungen. " +
                          "Erstelle Root-Lots für Fiat-Käufe und ordne Transfers/Swaps/Verkäufe zu. " +
                          "Nutze FIFO als Default und frage nur, wenn FIFO nicht möglich ist."
            });

            var lastRemaining = session.TotalCount;
            var idleIterations = 0;

            while (!linkedCt.Token.IsCancellationRequested)
            {
                var allowMemorySave = session.PendingMemorySave;
                session.PendingMemorySave = false;

                var prompt = BuildAgentInput(session, allowMemorySave);

                using (_contextAccessor.Use(new LotLinkingAgentContext(
                           session,
                           evt => SendEventAsync(session, evt),
                           allowMemorySave,
                           allowQuestions: true)))
                {
                    var result = await agent.RunAsync(prompt, cancellationToken: linkedCt.Token);
                    if (!string.IsNullOrWhiteSpace(result.Text))
                    {
                        session.History.Add(new ChatMessageDTO { Role = "assistant", Content = result.Text ?? "" });
                    }
                }

                if (session.CurrentQuestionId != null)
                {
                    var response = await session.UserResponseChannel.DequeueAsync(linkedCt.Token);
                    session.PendingMemorySave = response.ShouldRemember;
                    session.History.Add(new ChatMessageDTO { Role = "user", Content = FormatUserResponse(response) });
                    ResetQuestion(session);
                    continue;
                }

                var remaining = await GetPendingAssignmentCountAsync(dbContext, linkedCt.Token);
                session.ProcessedCount = Math.Max(0, session.TotalCount - remaining);
                await SendSessionUpdateAsync(session);

                if (remaining == 0)
                {
                    break;
                }

                if (remaining == lastRemaining)
                {
                    idleIterations++;
                    if (idleIterations >= 2)
                    {
                        await SendEventAsync(session, new LotLinkingEventDTO
                        {
                            EventType = "info",
                            Message = "Keine weiteren sicheren Zuordnungen gefunden. Bitte manuell prüfen.",
                            ProcessedCount = session.ProcessedCount,
                            TotalCount = session.TotalCount
                        });
                        break;
                    }
                }
                else
                {
                    idleIterations = 0;
                    lastRemaining = remaining;
                }

                session.History.Add(new ChatMessageDTO
                {
                    Role = "user",
                    Content = "Bitte fahre mit den verbleibenden Zuordnungen fort."
                });
            }

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

    private static string BuildAgentInput(InteractiveLotLinkingSession session, bool allowMemorySave)
    {
        var sb = new StringBuilder();

        sb.AppendLine("SESSION-HISTORIE (gekürzt):");
        foreach (var msg in session.History.TakeLast(20))
        {
            sb.AppendLine($"{msg.Role.ToUpperInvariant()}: {msg.Content}");
        }

        sb.AppendLine();
        sb.AppendLine("STATUS:");
        sb.AppendLine($"- Total pending: {session.TotalCount}");
        sb.AppendLine($"- Assigned: {session.AssignedCount}");
        sb.AppendLine($"- Created lots: {session.CreatedLotsCount}");
        sb.AppendLine($"- Skipped: {session.SkippedCount}");
        sb.AppendLine($"- Memory save allowed: {allowMemorySave}");
        sb.AppendLine();
        sb.AppendLine("ANWEISUNG:");
        sb.AppendLine("Nutze FIFO als Default. Frage nur, wenn FIFO nicht möglich ist.");

        return sb.ToString();
    }

    private static string FormatUserResponse(LotLinkingUserResponseDTO response)
    {
        var sb = new StringBuilder();
        sb.AppendLine("USER_RESPONSE");
        sb.AppendLine($"QuestionId: {response.QuestionId}");
        sb.AppendLine($"Response: {response.Response}");
        if (!string.IsNullOrWhiteSpace(response.CustomText))
        {
            sb.AppendLine($"CustomText: {response.CustomText}");
        }
        if (response.LotAllocations != null && response.LotAllocations.Count > 0)
        {
            sb.AppendLine("LotAllocations:");
            foreach (var allocation in response.LotAllocations)
            {
                sb.AppendLine($"- LotId: {allocation.LotId}, Quantity: {allocation.Quantity}");
            }
        }
        sb.AppendLine($"ShouldRemember: {response.ShouldRemember}");
        return sb.ToString();
    }

    private static void ResetQuestion(InteractiveLotLinkingSession session)
    {
        session.CurrentQuestionId = null;
        session.CurrentQuestion = null;
        session.CurrentAssignment = null;
        session.CurrentOptions = null;
        session.CurrentLotOptions = null;
    }

    private static async Task<int> GetPendingAssignmentCountAsync(CryptoTrackerDbContext dbContext, CancellationToken ct)
    {
        var pendingReceive = await dbContext.CryptoTransactions
            .CountAsync(t => t.TransactionType == TransactionType.Receive && !t.LotAssignmentConfirmed, ct);
        var pendingSell = await dbContext.CryptoTrades
            .CountAsync(t => t.TradeType == TradeType.Sell && !t.LotAssignmentConfirmed, ct);
        var pendingBuy = await dbContext.CryptoTrades
            .CountAsync(t => t.TradeType == TradeType.Buy
                          && t.ResultingLotId == null
                          && FiatSymbols.ForQuery.Contains(t.OppositeSymbol), ct);

        return pendingReceive + pendingSell + pendingBuy;
    }

    private static async Task<IList<LotLinkingRuleDTO>> LoadLearnedRulesAsync(
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

    private static async Task<LotLinkingStatisticsDTO> GetStatisticsAsync(
        CryptoTrackerDbContext dbContext,
        CancellationToken ct)
    {
        var pendingReceive = await dbContext.CryptoTransactions
            .CountAsync(t => t.TransactionType == TransactionType.Receive && !t.LotAssignmentConfirmed, ct);
        var pendingSell = await dbContext.CryptoTrades
            .CountAsync(t => t.TradeType == TradeType.Sell && !t.LotAssignmentConfirmed, ct);
        var pendingBuy = await dbContext.CryptoTrades
            .CountAsync(t => t.TradeType == TradeType.Buy
                          && t.ResultingLotId == null
                          && FiatSymbols.ForQuery.Contains(t.OppositeSymbol), ct);
        var completedTx = await dbContext.CryptoTransactions
            .CountAsync(t => t.LotAssignmentConfirmed, ct);
        var totalLots = await dbContext.AssetLots.CountAsync(ct);

        return new LotLinkingStatisticsDTO
        {
            TotalPendingAssignments = pendingReceive + pendingSell + pendingBuy,
            PendingReceiveTransactions = pendingReceive,
            PendingSellTrades = pendingSell,
            PendingBuyTrades = pendingBuy,
            CompletedAssignments = completedTx,
            LotsCreated = totalLots
        };
    }

    private Task SendEventAsync(InteractiveLotLinkingSession session, LotLinkingEventDTO evt)
    {
        return _hubContext.Clients.Group(session.SessionId)
            .SendAsync("OnLotLinkingEvent", evt);
    }

    private Task SendSessionUpdateAsync(InteractiveLotLinkingSession session)
    {
        return _hubContext.Clients.Group(session.SessionId)
            .SendAsync("OnLotLinkingSessionUpdate", session.ToDTO());
    }
}

/// <summary>
/// Interne Session-Klasse
/// </summary>
public sealed class InteractiveLotLinkingSession
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
    public IList<LotOptionDTO>? CurrentLotOptions { get; set; }
    public bool PendingMemorySave { get; set; }
    public CancellationTokenSource CancellationSource { get; } = new();
    internal AsyncQueue<LotLinkingUserResponseDTO> UserResponseChannel { get; init; } = default!;
    public List<ChatMessageDTO> History { get; } = new();

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
        CurrentOptions = CurrentOptions,
        CurrentLotOptions = CurrentLotOptions
    };
}
