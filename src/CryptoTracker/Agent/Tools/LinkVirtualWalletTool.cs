using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using CryptoTracker.Shared;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool um ein virtuelles Gegenstück zu erstellen und zu verknüpfen.
/// </summary>
public sealed class LinkVirtualWalletTool : IAgentTool
{
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly ILinkingAgentContextAccessor _contextAccessor;

    public LinkVirtualWalletTool(
        CryptoTrackerDbContext dbContext,
        ILinkingAgentContextAccessor contextAccessor)
    {
        _dbContext = dbContext;
        _contextAccessor = contextAccessor;
    }

    public Delegate GetToolRunner() => LinkVirtualWalletAsync;
    public string GetToolName() => "link_virtual_wallet";
    public string GetToolDescription() => """
        Erstellt ein virtuelles Gegenstück und verknüpft es mit einer Transaktion.
        Parameter:
        - transactionId: ID der bestehenden Transaktion
        - virtualWalletId: Optional, ID eines vorhandenen virtuellen Wallets
        - virtualWalletName: Optional, Name eines neuen virtuellen Wallets
        - reason: Optional, Begründung für die Verknüpfung
        """;

    private async Task<string> LinkVirtualWalletAsync(
        [Description("Transaktions-ID")] int transactionId,
        [Description("Optional: Virtuelles Wallet ID")] int? virtualWalletId,
        [Description("Optional: Virtuelles Wallet Name")] string? virtualWalletName,
        [Description("Optional: Begründung")] string? reason = null)
    {
        var tx = await _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        if (tx == null)
        {
            return JsonSerializer.Serialize(new { success = false, error = $"Transaktion {transactionId} nicht gefunden" });
        }

        if (tx.OppositeTransactionId != null)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Transaktion ist bereits verknüpft" });
        }

        var virtualWallet = await ResolveVirtualWalletAsync(virtualWalletId, virtualWalletName);
        if (virtualWallet == null)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Virtuelles Wallet konnte nicht gefunden oder erstellt werden" });
        }

        var oppositeType = tx.TransactionType == TransactionType.Send ? TransactionType.Receive : TransactionType.Send;
        var oppositeQuantity = tx.TransactionType == TransactionType.Send ? tx.QuantityAfterFee : tx.Quantity;

        if (oppositeQuantity <= 0)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Ungültige Menge für virtuelles Gegenstück" });
        }

        var oppositeTransaction = new CryptoTransaction
        {
            WalletId = virtualWallet.Id,
            DateTime = tx.DateTime,
            TransactionType = oppositeType,
            Symbol = tx.Symbol,
            Quantity = oppositeQuantity,
            Fee = 0m,
            Comment = $"Virtuelles Gegenstück zu #{tx.Id} ({tx.Wallet.Name})",
            Address = tx.Address,
            Network = tx.Network,
            TransactionId = tx.TransactionId
        };

        _dbContext.CryptoTransactions.Add(oppositeTransaction);
        await _dbContext.SaveChangesAsync();

        tx.OppositeTransactionId = oppositeTransaction.Id;
        tx.OppositeWalletId = oppositeTransaction.WalletId;
        oppositeTransaction.OppositeTransactionId = tx.Id;
        oppositeTransaction.OppositeWalletId = tx.WalletId;

        var metadata = new TransactionLinkMetadata
        {
            TransactionId = tx.Id,
            LinkType = TransactionLinkType.AIAssisted,
            Confidence = 0.9m,
            Reason = reason ?? $"Virtuelles Wallet: {virtualWallet.Name}",
            LinkedAt = DateTimeOffset.UtcNow,
            IsConfirmed = true,
            ConfirmedAt = DateTimeOffset.UtcNow
        };
        _dbContext.TransactionLinkMetadata.Add(metadata);

        await _dbContext.SaveChangesAsync();

        var context = _contextAccessor.Current;
        if (context != null)
        {
            context.Session.LinkedCount++;
            context.Session.ProcessedCount = context.Session.LinkedCount +
                                            context.Session.MarkedExternalCount +
                                            context.Session.SkippedCount;

            await context.SendEventAsync(new LinkingEventDTO
            {
                EventType = "linked",
                Message = $"Verknüpft (virtuell): {tx.Symbol} {oppositeQuantity:F8} ({tx.Wallet.Name} → {virtualWallet.Name})",
                SendId = tx.TransactionType == TransactionType.Send ? tx.Id : oppositeTransaction.Id,
                ReceiveId = tx.TransactionType == TransactionType.Receive ? tx.Id : oppositeTransaction.Id,
                ProcessedCount = context.Session.ProcessedCount,
                TotalCount = context.Session.TotalCount
            });
        }

        return JsonSerializer.Serialize(new { success = true, virtualWalletId = virtualWallet.Id });
    }

    private async Task<Wallet?> ResolveVirtualWalletAsync(int? walletId, string? walletName)
    {
        if (walletId.HasValue)
        {
            return await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.Id == walletId.Value && w.IsVirtual);
        }

        var name = walletName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var existing = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.Name == name);

        if (existing != null)
        {
            return existing.IsVirtual ? existing : null;
        }

        var wallet = new Wallet
        {
            Name = name,
            IsVirtual = true
        };

        _dbContext.Wallets.Add(wallet);
        await _dbContext.SaveChangesAsync();
        return wallet;
    }
}
