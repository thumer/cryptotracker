using System.Collections.Concurrent;
using System.Text;
using CryptoTracker.Agent.Common;
using CryptoTracker.Agent.Definitions;
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
    private readonly ILinkingAgentContextAccessor _contextAccessor;

    // Aktive Sessions
    private readonly ConcurrentDictionary<string, InteractiveLinkingSession> _sessions = new();

    public InteractiveLinkingService(
        IServiceScopeFactory scopeFactory,
        IHubContext<LinkingHub> hubContext,
        ILogger<InteractiveLinkingService> logger,
        ILinkingAgentContextAccessor contextAccessor)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
        _contextAccessor = contextAccessor;
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
    /// Hauptprozess für interaktives Linking (agentisch, iterativ)
    /// </summary>
    private async Task RunLinkingProcessAsync(InteractiveLinkingSession session, CancellationToken ct)
    {
        var linkedCt = CancellationTokenSource.CreateLinkedTokenSource(ct, session.CancellationSource.Token);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CryptoTrackerDbContext>();
            var agentBuilder = scope.ServiceProvider.GetRequiredService<AILinkingAgentBuilder>();

            if (!agentBuilder.IsConfigured())
            {
                await SendEventAsync(session, new LinkingEventDTO
                {
                    EventType = "error",
                    Message = "AI-Service nicht konfiguriert. Bitte OpenAI-Einstellungen prüfen.",
                    ProcessedCount = 0,
                    TotalCount = 0
                });
                return;
            }

            session.TotalCount = await GetRemainingUnlinkedCountAsync(dbContext, linkedCt.Token);
            await SendSessionUpdateAsync(session);

            var agent = agentBuilder.BuildAgent(TransactionLinkingAgentDefinition.KEY);

            session.History.Add(new ChatMessageDTO
            {
                Role = "user",
                Content = "Starte eine neue Transaktions-Verknüpfungs-Session. " +
                          "Lade gespeicherte Regeln und alle unverknüpften Transaktionen. " +
                          "Verknüpfe sichere Fälle automatisch. Frage bei Unsicherheit den Benutzer mit ask_user."
            });

            var lastRemaining = session.TotalCount;
            var idleIterations = 0;

            while (!linkedCt.Token.IsCancellationRequested)
            {
                var allowMemorySave = session.PendingMemorySave;
                session.PendingMemorySave = false;

                var prompt = BuildAgentInput(session, allowMemorySave);

                using (_contextAccessor.Use(new LinkingAgentContext(
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

                var remaining = await GetRemainingUnlinkedCountAsync(dbContext, linkedCt.Token);
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
                        await SendEventAsync(session, new LinkingEventDTO
                        {
                            EventType = "info",
                            Message = "Keine weiteren sicheren Verknüpfungen gefunden. Bitte manuell prüfen.",
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
                    Content = "Bitte fahre mit den verbleibenden Transaktionen fort."
                });
            }

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

    private static string BuildAgentInput(InteractiveLinkingSession session, bool allowMemorySave)
    {
        var sb = new StringBuilder();

        sb.AppendLine("SESSION-HISTORIE (gekürzt):");
        foreach (var msg in session.History.TakeLast(20))
        {
            sb.AppendLine($"{msg.Role.ToUpperInvariant()}: {msg.Content}");
        }

        sb.AppendLine();
        sb.AppendLine("STATUS:");
        sb.AppendLine($"- Total unlinked: {session.TotalCount}");
        sb.AppendLine($"- Linked: {session.LinkedCount}");
        sb.AppendLine($"- Marked external: {session.MarkedExternalCount}");
        sb.AppendLine($"- Skipped: {session.SkippedCount}");
        sb.AppendLine($"- Memory save allowed: {allowMemorySave}");
        sb.AppendLine();
        sb.AppendLine("ANWEISUNG:");
        sb.AppendLine("Verknüpfe sichere Fälle automatisch. Frage bei Unsicherheit den Benutzer mit ask_user.");

        return sb.ToString();
    }

    private static string FormatUserResponse(UserResponseDTO response)
    {
        var sb = new StringBuilder();
        sb.AppendLine("USER_RESPONSE");
        sb.AppendLine($"QuestionId: {response.QuestionId}");
        sb.AppendLine($"Response: {response.Response}");
        if (!string.IsNullOrWhiteSpace(response.Action))
        {
            sb.AppendLine($"Action: {response.Action}");
        }
        if (response.VirtualWalletId.HasValue)
        {
            sb.AppendLine($"VirtualWalletId: {response.VirtualWalletId}");
        }
        if (!string.IsNullOrWhiteSpace(response.VirtualWalletName))
        {
            sb.AppendLine($"VirtualWalletName: {response.VirtualWalletName}");
        }
        sb.AppendLine($"ShouldRemember: {response.ShouldRemember}");
        return sb.ToString();
    }

    private static void ResetQuestion(InteractiveLinkingSession session)
    {
        session.CurrentQuestionId = null;
        session.CurrentQuestion = null;
        session.CurrentTransaction = null;
        session.CurrentOptions = null;
    }

    private static Task<int> GetRemainingUnlinkedCountAsync(CryptoTrackerDbContext dbContext, CancellationToken ct)
    {
        return dbContext.CryptoTransactions
            .CountAsync(t => t.OppositeTransactionId == null && !t.IsIntentionallyUnlinked, ct);
    }

    private static async Task<LinkingStatisticsDTO> GetStatisticsAsync(CryptoTrackerDbContext dbContext, CancellationToken ct)
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

    private Task SendEventAsync(InteractiveLinkingSession session, LinkingEventDTO evt)
    {
        return _hubContext.Clients.Group(session.SessionId)
            .SendAsync("OnLinkingEvent", evt);
    }

    private Task SendSessionUpdateAsync(InteractiveLinkingSession session)
    {
        return _hubContext.Clients.Group(session.SessionId)
            .SendAsync("OnSessionUpdate", session.ToDTO());
    }
}

/// <summary>
/// Interne Session-Klasse
/// </summary>
public sealed class InteractiveLinkingSession
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
    public bool PendingMemorySave { get; set; }
    public CancellationTokenSource CancellationSource { get; } = new();
    internal AsyncQueue<UserResponseDTO> UserResponseChannel { get; init; } = default!;
    public List<ChatMessageDTO> History { get; } = new();

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
