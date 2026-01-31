using CryptoTracker.Agent.Services;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.SignalR;

namespace CryptoTracker.Hubs;

/// <summary>
/// SignalR Hub für Live-Updates während des interaktiven Linking-Prozesses
/// (Transaktions-Verknüpfung und Lot-Verknüpfung)
/// </summary>
public class LinkingHub : Hub
{
    private readonly InteractiveLinkingService _linkingService;
    private readonly InteractiveLotLinkingService _lotLinkingService;

    public LinkingHub(
        InteractiveLinkingService linkingService,
        InteractiveLotLinkingService lotLinkingService)
    {
        _linkingService = linkingService;
        _lotLinkingService = lotLinkingService;
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
    /// User sendet Antwort auf Agent-Frage (Transaktions-Verknüpfung)
    /// </summary>
    public async Task SendUserResponse(string sessionId, UserResponseDTO response)
    {
        // Weiterleiten an den InteractiveLinkingService
        await _linkingService.SubmitUserResponseAsync(sessionId, response);
    }

    /// <summary>
    /// User sendet Antwort auf Agent-Frage (Lot-Verknüpfung)
    /// </summary>
    public async Task SendLotLinkingResponse(string sessionId, LotLinkingUserResponseDTO response)
    {
        // Weiterleiten an den InteractiveLotLinkingService
        await _lotLinkingService.SubmitUserResponseAsync(sessionId, response);
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
