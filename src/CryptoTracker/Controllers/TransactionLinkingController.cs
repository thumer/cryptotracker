using CryptoTracker.Agent.Services;
using CryptoTracker.Entities;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionLinkingController : ControllerBase, ITransactionLinkingApi
{
    private readonly TransactionLinkingService _linkingService;
    private readonly InteractiveLinkingService _interactiveService;
    private readonly CryptoTrackerDbContext _dbContext;

    public TransactionLinkingController(
        TransactionLinkingService linkingService,
        InteractiveLinkingService interactiveService,
        CryptoTrackerDbContext dbContext)
    {
        _linkingService = linkingService;
        _interactiveService = interactiveService;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Prüft ob der AI-Service konfiguriert ist
    /// </summary>
    [HttpGet("status")]
    public async Task<ServiceStatusDTO> GetStatusAsync()
    {
        await Task.CompletedTask;
        return new ServiceStatusDTO
        {
            IsConfigured = _linkingService.IsConfigured,
            Message = _linkingService.IsConfigured
                ? "AI-Service ist bereit"
                : "AI-Service nicht konfiguriert. Bitte OpenAI-Einstellungen prüfen."
        };
    }

    /// <summary>
    /// Gibt Statistiken über Verknüpfungen zurück
    /// </summary>
    [HttpGet("statistics")]
    public async Task<LinkingStatisticsDTO> GetStatisticsAsync()
    {
        var stats = await _linkingService.GetStatisticsAsync();
        return new LinkingStatisticsDTO
        {
            TotalTransactions = stats.TotalTransactions,
            LinkedTransactions = stats.LinkedTransactions,
            IntentionallyUnlinked = stats.IntentionallyUnlinked,
            UnlinkedTransactions = stats.UnlinkedTransactions,
            ConfirmedLinks = stats.ConfirmedLinks,
            UnconfirmedLinks = stats.UnconfirmedLinks,
            UnlinkedBySymbol = stats.UnlinkedBySymbol
        };
    }

    /// <summary>
    /// Startet eine neue Agent-Session
    /// </summary>
    [HttpPost("session/start")]
    public async Task<LinkingSessionResultDTO> StartSessionAsync()
    {
        var result = await _linkingService.StartSessionAsync();
        return new LinkingSessionResultDTO
        {
            AgentResponse = result.AgentResponse,
            Success = result.Success,
            Proposals = result.Proposals?.Select(p => new TransactionLinkProposalDTO
            {
                SendId = p.SendId,
                ReceiveId = p.ReceiveId,
                Confidence = p.Confidence,
                Reason = p.Reason
            }).ToList()
        };
    }

    /// <summary>
    /// Sendet eine Nachricht an den Agent
    /// </summary>
    [HttpPost("session/message")]
    public async Task<LinkingSessionResultDTO> SendMessageAsync([FromBody] string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new LinkingSessionResultDTO
            {
                AgentResponse = "Nachricht darf nicht leer sein",
                Success = false
            };
        }

        var result = await _linkingService.ContinueSessionAsync(message);
        return new LinkingSessionResultDTO
        {
            AgentResponse = result.AgentResponse,
            Success = result.Success,
            Proposals = result.Proposals?.Select(p => new TransactionLinkProposalDTO
            {
                SendId = p.SendId,
                ReceiveId = p.ReceiveId,
                Confidence = p.Confidence,
                Reason = p.Reason
            }).ToList()
        };
    }

    /// <summary>
    /// Führt automatische Verknüpfung durch
    /// </summary>
    [HttpPost("auto-link")]
    public async Task<AutoLinkResultDTO> RunAutoLinkAsync()
    {
        var result = await _linkingService.RunAutomaticLinkingAsync();
        return new AutoLinkResultDTO
        {
            LinkedCount = result.LinkedCount,
            MarkedUnlinkedCount = result.MarkedUnlinkedCount,
            RemainingUnlinkedCount = result.RemainingUnlinkedCount,
            Summary = result.Summary,
            Success = result.Success
        };
    }

    /// <summary>
    /// Gibt alle unverknüpften Transaktionen zurück
    /// </summary>
    [HttpGet("unlinked")]
    public async Task<IList<UnlinkedTransactionDTO>> GetUnlinkedAsync(
        [FromQuery] string? type = null,
        [FromQuery] string? symbol = null,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0)
    {
        var query = _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Where(t => t.OppositeTransactionId == null && !t.IsIntentionallyUnlinked);

        if (type?.Equals("send", StringComparison.OrdinalIgnoreCase) == true)
            query = query.Where(t => t.TransactionType == TransactionType.Send);
        else if (type?.Equals("receive", StringComparison.OrdinalIgnoreCase) == true)
            query = query.Where(t => t.TransactionType == TransactionType.Receive);

        if (!string.IsNullOrEmpty(symbol))
            query = query.Where(t => t.Symbol == symbol);

        var transactions = await query
            .OrderByDescending(t => t.DateTime)
            .Skip(offset)
            .Take(limit)
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
            .ToListAsync();

        return transactions;
    }

    /// <summary>
    /// Manuell zwei Transaktionen verknüpfen
    /// </summary>
    [HttpPost("link")]
    public async Task<LinkResultDTO> ManualLinkAsync([FromQuery] int sendId, [FromQuery] int receiveId, [FromQuery] string? reason = null)
    {
        var send = await _dbContext.CryptoTransactions.FindAsync(sendId);
        var receive = await _dbContext.CryptoTransactions.FindAsync(receiveId);

        if (send == null)
            return new LinkResultDTO { Success = false, Message = $"Send-Transaktion {sendId} nicht gefunden" };
        if (receive == null)
            return new LinkResultDTO { Success = false, Message = $"Receive-Transaktion {receiveId} nicht gefunden" };

        if (send.TransactionType != TransactionType.Send)
            return new LinkResultDTO { Success = false, Message = $"Transaktion {sendId} ist kein Send" };
        if (receive.TransactionType != TransactionType.Receive)
            return new LinkResultDTO { Success = false, Message = $"Transaktion {receiveId} ist kein Receive" };

        if (send.OppositeTransactionId != null)
            return new LinkResultDTO { Success = false, Message = "Send-Transaktion ist bereits verknüpft" };
        if (receive.OppositeTransactionId != null)
            return new LinkResultDTO { Success = false, Message = "Receive-Transaktion ist bereits verknüpft" };

        send.OppositeTransactionId = receive.Id;
        send.OppositeWalletId = receive.WalletId;
        receive.OppositeTransactionId = send.Id;
        receive.OppositeWalletId = send.WalletId;

        var metadata = new TransactionLinkMetadata
        {
            TransactionId = send.Id,
            LinkType = TransactionLinkType.Manual,
            Confidence = 1.0m,
            Reason = reason ?? "Manuell verknüpft",
            LinkedAt = DateTimeOffset.UtcNow,
            IsConfirmed = true,
            ConfirmedAt = DateTimeOffset.UtcNow
        };
        _dbContext.TransactionLinkMetadata.Add(metadata);

        await _dbContext.SaveChangesAsync();
        return new LinkResultDTO { Success = true, Message = "Transaktionen verknüpft" };
    }

    /// <summary>
    /// Bestätigt vorgeschlagene Verknüpfungen
    /// </summary>
    [HttpPost("confirm")]
    public async Task<ConfirmResultDTO> ConfirmLinksAsync([FromBody] IList<int> transactionIds)
    {
        var metadataList = await _dbContext.TransactionLinkMetadata
            .Where(m => transactionIds.Contains(m.TransactionId))
            .ToListAsync();

        foreach (var metadata in metadataList)
        {
            metadata.IsConfirmed = true;
            metadata.ConfirmedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
        return new ConfirmResultDTO { ConfirmedCount = metadataList.Count };
    }

    /// <summary>
    /// Setzt alle Verknüpfungen zurück
    /// </summary>
    [HttpPost("reset")]
    public async Task<ResetResultDTO> ResetLinksAsync(
        [FromQuery] bool keepManualLinks = true,
        [FromQuery] bool resetIntentionallyUnlinked = false)
    {
        var result = await _linkingService.ResetAllLinksAsync(
            keepManualLinks,
            resetIntentionallyUnlinked);

        return new ResetResultDTO
        {
            ResetCount = result.ResetCount,
            MetadataDeleted = result.MetadataDeleted
        };
    }

    /// <summary>
    /// Markiert eine Transaktion als absichtlich unverknüpft (externe Einnahme/Ausgabe)
    /// </summary>
    [HttpPost("mark-unlinked")]
    public async Task<LinkResultDTO> MarkAsIntentionallyUnlinkedAsync([FromQuery] int transactionId, [FromQuery] string reason)
    {
        var transaction = await _dbContext.CryptoTransactions.FindAsync(transactionId);
        if (transaction == null)
            return new LinkResultDTO { Success = false, Message = $"Transaktion {transactionId} nicht gefunden" };

        if (transaction.OppositeTransactionId != null)
            return new LinkResultDTO { Success = false, Message = "Transaktion ist bereits verknüpft" };

        transaction.IsIntentionallyUnlinked = true;

        var metadata = new TransactionLinkMetadata
        {
            TransactionId = transactionId,
            LinkType = TransactionLinkType.IntentionallyUnlinked | TransactionLinkType.Manual,
            Confidence = 1.0m,
            Reason = reason,
            LinkedAt = DateTimeOffset.UtcNow,
            IsConfirmed = true,
            ConfirmedAt = DateTimeOffset.UtcNow
        };
        _dbContext.TransactionLinkMetadata.Add(metadata);

        await _dbContext.SaveChangesAsync();
        return new LinkResultDTO { Success = true, Message = "Transaktion als externe Einnahme/Ausgabe markiert" };
    }

    // === Neue interaktive Linking-Methoden ===

    /// <summary>
    /// Startet eine neue interaktive Linking-Session
    /// </summary>
    [HttpPost("interactive/start")]
    public async Task<InteractiveLinkingSessionDTO> StartInteractiveSessionAsync()
    {
        return await _interactiveService.StartSessionAsync();
    }

    /// <summary>
    /// Gibt den Status einer interaktiven Session zurück
    /// </summary>
    [HttpGet("interactive/{sessionId}/status")]
    public Task<InteractiveLinkingSessionDTO?> GetInteractiveSessionStatusAsync(string sessionId)
    {
        return Task.FromResult(_interactiveService.GetSessionStatus(sessionId));
    }

    /// <summary>
    /// Sendet User-Antwort an interaktive Session
    /// </summary>
    [HttpPost("interactive/{sessionId}/respond")]
    public async Task SubmitUserResponseAsync(string sessionId, [FromBody] UserResponseDTO response)
    {
        await _interactiveService.SubmitUserResponseAsync(sessionId, response);
    }

    /// <summary>
    /// Stoppt eine interaktive Session
    /// </summary>
    [HttpPost("interactive/{sessionId}/stop")]
    public Task StopInteractiveSessionAsync(string sessionId)
    {
        _interactiveService.StopSession(sessionId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gibt den Linking-Context (alle Daten + Hints) zurück
    /// </summary>
    [HttpGet("context")]
    public async Task<LinkingContextDTO> GetLinkingContextAsync()
    {
        // Lade unverknüpfte Transaktionen
        var unlinked = await _dbContext.CryptoTransactions
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
            .ToListAsync();

        // Berechne Hints
        var hints = CalculateHints(unlinked);

        // Lade Regeln
        var rules = await GetLearnedRulesAsync();

        // Statistiken
        var stats = await GetStatisticsAsync();

        return new LinkingContextDTO
        {
            UnlinkedTransactions = unlinked,
            Hints = hints,
            LearnedRules = rules,
            Statistics = stats
        };
    }

    /// <summary>
    /// Gibt gelernte Regeln zurück
    /// </summary>
    [HttpGet("rules")]
    public async Task<IList<LearnedRuleDTO>> GetLearnedRulesAsync()
    {
        var memories = await _dbContext.AgentMemories
            .Where(m => m.AgentKey == "transaction-linking" && m.Key.StartsWith("rule:"))
            .ToListAsync();

        return memories
            .Select(m => System.Text.Json.JsonSerializer.Deserialize<LearnedRuleDTO>(m.Value))
            .Where(r => r != null)
            .Cast<LearnedRuleDTO>()
            .ToList();
    }

    /// <summary>
    /// Löscht eine gelernte Regel
    /// </summary>
    [HttpDelete("rules/{ruleId}")]
    public async Task<bool> DeleteLearnedRuleAsync(string ruleId)
    {
        var memory = await _dbContext.AgentMemories
            .FirstOrDefaultAsync(m => m.AgentKey == "transaction-linking" && m.Key == $"rule:{ruleId}");

        if (memory == null)
            return false;

        _dbContext.AgentMemories.Remove(memory);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Berechnet potentielle Verknüpfungs-Hints
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
                if (!string.Equals(send.Symbol, receive.Symbol, StringComparison.OrdinalIgnoreCase))
                    continue;

                var timeDiff = receive.DateTime - send.DateTime;
                if (timeDiff < TimeSpan.FromMinutes(-5) || timeDiff > TimeSpan.FromHours(24))
                    continue;

                var amountDiff = Math.Abs(send.QuantityAfterFee - receive.Quantity);
                var amountPercent = send.QuantityAfterFee > 0 
                    ? amountDiff / send.QuantityAfterFee 
                    : 1;

                var confidence = CalculateConfidence(timeDiff, amountPercent);

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

    private decimal CalculateConfidence(TimeSpan timeDiff, decimal amountPercentDiff)
    {
        var score = 0.5m;

        if (timeDiff.TotalMinutes <= 5) score += 0.25m;
        else if (timeDiff.TotalMinutes <= 30) score += 0.20m;
        else if (timeDiff.TotalHours <= 1) score += 0.15m;
        else if (timeDiff.TotalHours <= 6) score += 0.10m;
        else score += 0.05m;

        if (amountPercentDiff == 0) score += 0.25m;
        else if (amountPercentDiff < 0.001m) score += 0.20m;
        else if (amountPercentDiff < 0.01m) score += 0.15m;
        else if (amountPercentDiff < 0.05m) score += 0.10m;
        else score += 0.05m;

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
}
