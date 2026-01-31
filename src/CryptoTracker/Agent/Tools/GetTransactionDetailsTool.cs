using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool zum Laden von Transaktionsdetails
/// </summary>
public sealed class GetTransactionDetailsTool : IAgentTool
{
    private readonly CryptoTrackerDbContext _dbContext;

    public GetTransactionDetailsTool(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Delegate GetToolRunner() => GetTransactionDetailsAsync;
    public string GetToolName() => "get_transaction_details";
    public string GetToolDescription() => """
        Lädt Details zu einer oder mehreren Transaktionen.
        Parameter:
        - transactionIds: Komma-separierte Liste von Datenbank-IDs (z.B. "1,2,3")
        
        Gibt JSON mit vollständigen Details zurück inkl. verknüpfter Transaktion falls vorhanden.
        Die "Id" ist die Datenbank-ID, "TxHash" ist der Blockchain Transaction-Hash.
        """;

    private async Task<string> GetTransactionDetailsAsync(
        [Description("Komma-separierte Liste von IDs, z.B. '1,2,3'")] string transactionIds)
    {
        var ids = transactionIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        if (!ids.Any())
            return JsonSerializer.Serialize(new { error = "Keine gültigen IDs angegeben" });

        var transactions = await _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Include(t => t.OppositeTransaction)
                .ThenInclude(ot => ot!.Wallet)
            .Include(t => t.LinkMetadata)
            .Where(t => ids.Contains(t.Id))
            .Select(t => new
            {
                t.Id,  // Datenbank-ID
                DateTime = t.DateTime.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                Type = t.TransactionType.ToString(),
                t.Symbol,
                t.Quantity,
                t.QuantityAfterFee,
                t.Fee,
                t.Comment,
                t.Address,
                TxHash = t.TransactionId,  // Blockchain TX-Hash
                t.Network,
                WalletId = t.WalletId,
                WalletName = t.Wallet.Name,
                t.IsIntentionallyUnlinked,
                OppositeTransaction = t.OppositeTransaction != null ? new
                {
                    t.OppositeTransaction.Id,
                    DateTime = t.OppositeTransaction.DateTime.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                    Type = t.OppositeTransaction.TransactionType.ToString(),
                    t.OppositeTransaction.Symbol,
                    t.OppositeTransaction.Quantity,
                    t.OppositeTransaction.QuantityAfterFee,
                    WalletName = t.OppositeTransaction.Wallet.Name
                } : null,
                LinkMetadata = t.LinkMetadata != null ? new
                {
                    LinkType = t.LinkMetadata.LinkType.ToString(),
                    t.LinkMetadata.Confidence,
                    t.LinkMetadata.Reason,
                    t.LinkMetadata.IsConfirmed,
                    LinkedAt = t.LinkMetadata.LinkedAt.ToString("yyyy-MM-dd HH:mm:ss")
                } : null
            })
            .ToListAsync();

        return JsonSerializer.Serialize(transactions, new JsonSerializerOptions { WriteIndented = false });
    }
}
