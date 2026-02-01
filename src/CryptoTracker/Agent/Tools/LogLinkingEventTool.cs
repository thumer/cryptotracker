using CryptoTracker.Agent.Common;
using CryptoTracker.Shared;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool für Info/Status-Events während des Linking-Prozesses.
/// </summary>
public sealed class LogLinkingEventTool : IAgentTool
{
    private readonly ILinkingAgentContextAccessor _contextAccessor;

    public LogLinkingEventTool(ILinkingAgentContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public Delegate GetToolRunner() => LogEventAsync;
    public string GetToolName() => "log_linking_event";
    public string GetToolDescription() => """
        Sendet eine Status-/Info-Nachricht an die UI.
        Parameter:
        - message: Text der Info
        - eventType: Optional, z.B. "info" oder "progress" (default: "info")
        """;

    private async Task<string> LogEventAsync(
        [Description("Nachricht")] string message,
        [Description("EventType, z.B. info/progress")] string? eventType = null)
    {
        var context = _contextAccessor.Current;
        if (context == null)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Kein aktiver Session-Kontext" });
        }

        var type = string.IsNullOrWhiteSpace(eventType) ? "info" : eventType.Trim();

        await context.SendEventAsync(new LinkingEventDTO
        {
            EventType = type,
            Message = message,
            ProcessedCount = context.Session.ProcessedCount,
            TotalCount = context.Session.TotalCount
        });

        return JsonSerializer.Serialize(new { success = true });
    }
}
