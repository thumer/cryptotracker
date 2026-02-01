using CryptoTracker.Agent.Common;
using CryptoTracker.Shared;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool um eine Lot-Zuordnung explizit zu überspringen (Session-Logik).
/// </summary>
public sealed class SkipLotAssignmentTool : IAgentTool
{
    private readonly ILotLinkingAgentContextAccessor _contextAccessor;

    public SkipLotAssignmentTool(ILotLinkingAgentContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public Delegate GetToolRunner() => SkipAssignmentAsync;
    public string GetToolName() => "skip_lot_assignment";
    public string GetToolDescription() => """
        Überspringt eine Lot-Zuordnung in der aktuellen Session (keine DB-Änderung).
        Parameter:
        - assignmentId: ID des Eintrags
        - reason: Kurzbegründung
        """;

    private async Task<string> SkipAssignmentAsync(
        [Description("Assignment ID")] int assignmentId,
        [Description("Begründung")] string reason)
    {
        var context = _contextAccessor.Current;
        if (context == null)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Kein aktiver Session-Kontext" });
        }

        context.Session.SkippedCount++;
        context.Session.ProcessedCount = context.Session.AssignedCount + context.Session.SkippedCount;

        await context.SendEventAsync(new LotLinkingEventDTO
        {
            EventType = "skipped",
            Message = $"Übersprungen: #{assignmentId} - {reason}",
            ProcessedCount = context.Session.ProcessedCount,
            TotalCount = context.Session.TotalCount
        });

        return JsonSerializer.Serialize(new { success = true });
    }
}
