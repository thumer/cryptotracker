using CryptoTracker.Agent.Services;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.SignalR;

namespace CryptoTracker.Hubs;

/// <summary>
/// SignalR Hub für Live-Updates während des interaktiven Linking-Prozesses
/// </summary>
public class LinkingHub : Hub
{
    private readonly InteractiveLinkingService _linkingService;

    public LinkingHub(InteractiveLinkingService linkingService)
    {
        _linkingService = linkingService;
    }

    /// <summary>
    /// Client verbindet sich mit einer Linking-Session
    /// </summary>
    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
    }

    /// <summary>
    /// Client verlässt eine Linking-Session
    /// </summary>
    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
    }

    /// <summary>
    /// User sendet Antwort auf Agent-Frage
    /// </summary>
    public async Task SendUserResponse(string sessionId, UserResponseDTO response)
    {
        // Weiterleiten an den InteractiveLinkingService
        await _linkingService.SubmitUserResponseAsync(sessionId, response);
    }
}

/// <summary>
/// Interface für das Senden von Events an Clients
/// </summary>
public interface ILinkingHubClient
{
    /// <summary>
    /// Sendet ein Linking-Event an alle Clients in der Session
    /// </summary>
    Task OnLinkingEvent(LinkingEventDTO linkingEvent);

    /// <summary>
    /// Sendet Session-Status-Update
    /// </summary>
    Task OnSessionUpdate(InteractiveLinkingSessionDTO session);

    /// <summary>
    /// Sendet Fehler
    /// </summary>
    Task OnError(string message);

    /// <summary>
    /// Session beendet
    /// </summary>
    Task OnSessionCompleted(LinkingStatisticsDTO finalStats);
}
