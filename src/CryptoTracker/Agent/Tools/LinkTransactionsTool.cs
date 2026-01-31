using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool zum Verknüpfen von zwei Transaktionen
/// </summary>
public sealed class LinkTransactionsTool : IAgentTool
{
    private readonly CryptoTrackerDbContext _dbContext;

    public LinkTransactionsTool(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Delegate GetToolRunner() => LinkTransactionsAsync;
    public string GetToolName() => "link_transactions";
    public string GetToolDescription() => """
        Verknüpft zwei Transaktionen miteinander (Send mit Receive).
        Parameter:
        - sendTransactionId: Datenbank-ID der Send-Transaktion (das "Id" Feld aus get_unlinked_transactions)
        - receiveTransactionId: Datenbank-ID der Receive-Transaktion (das "Id" Feld aus get_unlinked_transactions)
        - linkType: Wie wurde die Verknüpfung gefunden? Flags kombinierbar:
          1=TimeAndAmount, 2=AIAssisted, 4=Direct, 8=Indirect, 16=Automatic
        - confidence: Konfidenz 0.0-1.0 (wie sicher ist die Verknüpfung?)
        - reason: Begründung für die Verknüpfung
        
        Gibt Erfolg/Fehler-Status zurück.
        """;

    private async Task<string> LinkTransactionsAsync(
        [Description("Datenbank-ID der Send-Transaktion")] int sendTransactionId,
        [Description("Datenbank-ID der Receive-Transaktion")] int receiveTransactionId,
        [Description("Link-Typ Flags (1=TimeAndAmount, 2=AIAssisted, 4=Direct, 8=Indirect, 16=Automatic)")] int linkType,
        [Description("Konfidenz 0.0-1.0")] decimal confidence,
        [Description("Begründung für die Verknüpfung")] string reason)
    {
        var send = await _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.Id == sendTransactionId);

        var receive = await _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.Id == receiveTransactionId);

        if (send == null)
            return JsonSerializer.Serialize(new { success = false, error = $"Send-Transaktion {sendTransactionId} nicht gefunden" });

        if (receive == null)
            return JsonSerializer.Serialize(new { success = false, error = $"Receive-Transaktion {receiveTransactionId} nicht gefunden" });

        if (send.TransactionType != TransactionType.Send)
            return JsonSerializer.Serialize(new { success = false, error = $"Transaktion {sendTransactionId} ist kein Send (ist {send.TransactionType})" });

        if (receive.TransactionType != TransactionType.Receive)
            return JsonSerializer.Serialize(new { success = false, error = $"Transaktion {receiveTransactionId} ist kein Receive (ist {receive.TransactionType})" });

        if (send.OppositeTransactionId != null)
            return JsonSerializer.Serialize(new { success = false, error = $"Send-Transaktion {sendTransactionId} ist bereits verknüpft mit {send.OppositeTransactionId}" });

        if (receive.OppositeTransactionId != null)
            return JsonSerializer.Serialize(new { success = false, error = $"Receive-Transaktion {receiveTransactionId} ist bereits verknüpft mit {receive.OppositeTransactionId}" });

        if (send.Symbol != receive.Symbol)
            return JsonSerializer.Serialize(new { success = false, error = $"Symbol-Mismatch: Send={send.Symbol}, Receive={receive.Symbol}" });

        // Verknüpfung erstellen
        send.OppositeTransactionId = receive.Id;
        send.OppositeWalletId = receive.WalletId;
        receive.OppositeTransactionId = send.Id;
        receive.OppositeWalletId = send.WalletId;

        // Metadaten für Send speichern
        var sendMetadata = new TransactionLinkMetadata
        {
            TransactionId = send.Id,
            LinkType = (TransactionLinkType)linkType,
            Confidence = Math.Clamp(confidence, 0m, 1m),
            Reason = reason,
            LinkedAt = DateTimeOffset.UtcNow,
            IsConfirmed = false
        };
        _dbContext.TransactionLinkMetadata.Add(sendMetadata);

        // Metadaten für Receive speichern
        var receiveMetadata = new TransactionLinkMetadata
        {
            TransactionId = receive.Id,
            LinkType = (TransactionLinkType)linkType,
            Confidence = Math.Clamp(confidence, 0m, 1m),
            Reason = reason,
            LinkedAt = DateTimeOffset.UtcNow,
            IsConfirmed = false
        };
        _dbContext.TransactionLinkMetadata.Add(receiveMetadata);

        await _dbContext.SaveChangesAsync();

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Transaktionen verknüpft: Send #{send.Id} ({send.Wallet.Name}) ↔ Receive #{receive.Id} ({receive.Wallet.Name})",
            sendId = send.Id,
            receiveId = receive.Id,
            symbol = send.Symbol,
            sendAmount = send.QuantityAfterFee,
            receiveAmount = receive.Quantity
        });
    }
}
