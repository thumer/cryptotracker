using CryptoTracker.Agent.Common;
using CryptoTracker.Shared;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool für Info/Status-Events während des Lot-Linking.
/// </summary>
public sealed class LogLotLinkingEventTool : IAgentTool
{
    private readonly ILotLinkingAgentContextAccessor _contextAccessor;

    public LogLotLinkingEventTool(ILotLinkingAgentContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public Delegate GetToolRunner() => LogEventAsync;
    public string GetToolName() => "log_lot_event";
    public string GetToolDescription() => """
        Sendet eine Status-/Info-Nachricht an die UI.
        Parameter:
        - message: Text der Info
        - eventType: Optional, z.B. "info" oder "progress" (default: "info")
        """;

    private async Task<string> LogEventAsync(
        [Description("Nachricht")] string message,
        [Description("EventType")] string? eventType = null)
    {
        var context = _contextAccessor.Current;
        if (context == null)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Kein aktiver Session-Kontext" });
        }

        var type = string.IsNullOrWhiteSpace(eventType) ? "info" : eventType.Trim();

        await context.SendEventAsync(new LotLinkingEventDTO
        {
            EventType = type,
            Message = message,
            ProcessedCount = context.Session.ProcessedCount,
            TotalCount = context.Session.TotalCount
        });

        return JsonSerializer.Serialize(new { success = true });
    }
}
