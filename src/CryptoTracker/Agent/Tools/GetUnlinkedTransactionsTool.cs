using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool zum Laden unverknüpfter Transaktionen
/// </summary>
public sealed class GetUnlinkedTransactionsTool : IAgentTool
{
    private readonly CryptoTrackerDbContext _dbContext;

    public GetUnlinkedTransactionsTool(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Delegate GetToolRunner() => GetUnlinkedTransactionsAsync;
    public string GetToolName() => "get_unlinked_transactions";
    public string GetToolDescription() => """
        Lädt unverknüpfte Transaktionen aus der Datenbank.
        Parameter:
        - type: "send", "receive" oder "all" (default: "all")
        - symbol: Optional, z.B. "ETH", "BTC" zum Filtern nach Coin
        - limit: Max. Anzahl Ergebnisse (default: 50, max: 200)
        - offset: Für Paginierung (default: 0)
        - includeIntentionallyUnlinked: Auch bewusst unverknüpfte einschließen (default: false)
        
        Gibt JSON-Array zurück mit: 
        - Id: Datenbank-ID (diese ID für link_transactions verwenden!)
        - DateTime, Type, Symbol, Quantity, QuantityAfterFee, Comment, Address, WalletName
        - TxHash: Blockchain Transaction-Hash (kann leer sein, nur zur Info)
        """;

    private async Task<string> GetUnlinkedTransactionsAsync(
        [Description("Filter: 'send', 'receive' oder 'all'")] string type = "all",
        [Description("Symbol-Filter, z.B. 'ETH', 'BTC'")] string? symbol = null,
        [Description("Max. Anzahl Ergebnisse (1-200)")] int limit = 50,
        [Description("Offset für Paginierung")] int offset = 0,
        [Description("Auch bewusst unverknüpfte einschließen")] bool includeIntentionallyUnlinked = false)
    {
        limit = Math.Clamp(limit, 1, 200);
        offset = Math.Max(0, offset);

        var query = _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Where(t => t.OppositeTransactionId == null);

        if (!includeIntentionallyUnlinked)
            query = query.Where(t => !t.IsIntentionallyUnlinked);

        if (type.Equals("send", StringComparison.OrdinalIgnoreCase))
            query = query.Where(t => t.TransactionType == TransactionType.Send);
        else if (type.Equals("receive", StringComparison.OrdinalIgnoreCase))
            query = query.Where(t => t.TransactionType == TransactionType.Receive);

        if (!string.IsNullOrEmpty(symbol))
            query = query.Where(t => t.Symbol == symbol);

        var totalCount = await query.CountAsync();

        var transactions = await query
            .OrderBy(t => t.DateTime)
            .Skip(offset)
            .Take(limit)
            .Select(t => new
            {
                t.Id,  // Datenbank-ID - diese für link_transactions verwenden!
                DateTime = t.DateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Type = t.TransactionType.ToString(),
                t.Symbol,
                t.Quantity,
                t.QuantityAfterFee,
                t.Comment,
                t.Address,
                TxHash = t.TransactionId,  // Blockchain TX-Hash (kann leer sein)
                WalletName = t.Wallet.Name
            })
            .ToListAsync();

        var result = new
        {
            totalCount,
            offset,
            limit,
            transactions
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
    }
}
