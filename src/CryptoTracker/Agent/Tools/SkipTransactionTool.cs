using CryptoTracker.Agent.Common;
using CryptoTracker.Shared;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool um eine Transaktion explizit zu überspringen (Session-Logik).
/// </summary>
public sealed class SkipTransactionTool : IAgentTool
{
    private readonly ILinkingAgentContextAccessor _contextAccessor;

    public SkipTransactionTool(ILinkingAgentContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public Delegate GetToolRunner() => SkipTransactionAsync;
    public string GetToolName() => "skip_transaction";
    public string GetToolDescription() => """
        Überspringt eine Transaktion in der aktuellen Session (keine DB-Änderung).
        Parameter:
        - transactionId: ID der Transaktion
        - reason: Kurzbegründung warum übersprungen
        """;

    private async Task<string> SkipTransactionAsync(
        [Description("Transaktions-ID")] int transactionId,
        [Description("Begründung")] string reason)
    {
        var context = _contextAccessor.Current;
        if (context == null)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Kein aktiver Session-Kontext" });
        }

        context.Session.SkippedCount++;
        context.Session.ProcessedCount = context.Session.LinkedCount +
                                        context.Session.MarkedExternalCount +
                                        context.Session.SkippedCount;

        await context.SendEventAsync(new LinkingEventDTO
        {
            EventType = "skipped",
            Message = $"Übersprungen: #{transactionId} - {reason}",
            ProcessedCount = context.Session.ProcessedCount,
            TotalCount = context.Session.TotalCount
        });

        return JsonSerializer.Serialize(new { success = true });
    }
}
