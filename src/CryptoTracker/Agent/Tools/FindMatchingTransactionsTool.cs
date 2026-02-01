using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool zum Suchen von potentiellen Gegenstücken für eine Transaktion
/// </summary>
public sealed class FindMatchingTransactionsTool : IAgentTool
{
    private readonly CryptoTrackerDbContext _dbContext;

    public FindMatchingTransactionsTool(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Delegate GetToolRunner() => FindMatchingTransactionsAsync;
    public string GetToolName() => "find_matching_transactions";
    public string GetToolDescription() => """
        Sucht potentielle Gegenstücke für eine Transaktion basierend auf Zeit und Betrag.
        HINWEIS: Dieses Tool ist nur eine HILFE und findet oft KEINE Matches!
        Du solltest die Transaktionen SELBST analysieren und vergleichen.
        
        Parameter:
        - transactionId: Datenbank-ID der Transaktion für die Gegenstücke gesucht werden
        - timeWindowMinutes: Zeitfenster in Minuten (default: 60)
        - amountTolerance: Toleranz für Betragsabweichung in Prozent (default: 0.01 = 1%)
        
        Gibt Liste von potentiellen Matches mit Konfidenzwerten zurück.
        Die "Id" in den Ergebnissen ist die Datenbank-ID für link_transactions.
        """;

    private async Task<string> FindMatchingTransactionsAsync(
        [Description("Datenbank-ID der Transaktion")] int transactionId,
        [Description("Zeitfenster in Minuten")] int timeWindowMinutes = 60,
        [Description("Toleranz für Betragsabweichung (0.01 = 1%)")] decimal amountTolerance = 0.01m)
    {
        var sourceTransaction = await _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        if (sourceTransaction == null)
            return JsonSerializer.Serialize(new { error = "Transaktion nicht gefunden" });

        if (sourceTransaction.OppositeTransactionId != null)
            return JsonSerializer.Serialize(new { error = "Transaktion ist bereits verknüpft" });

        // Suche nach entgegengesetztem Typ
        var oppositeType = sourceTransaction.TransactionType == TransactionType.Send
            ? TransactionType.Receive
            : TransactionType.Send;

        var timeWindow = TimeSpan.FromMinutes(timeWindowMinutes);
        var minTime = sourceTransaction.DateTime - timeWindow;
        var maxTime = sourceTransaction.DateTime + timeWindow;

        // Bei Send: Vergleiche QuantityAfterFee mit Receive.Quantity
        // Bei Receive: Vergleiche Quantity mit Send.QuantityAfterFee
        var compareAmount = sourceTransaction.TransactionType == TransactionType.Send
            ? sourceTransaction.QuantityAfterFee
            : sourceTransaction.Quantity;

        var minAmount = compareAmount * (1 - amountTolerance);
        var maxAmount = compareAmount * (1 + amountTolerance);

        var candidates = await _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Where(t => t.Id != transactionId)
            .Where(t => t.OppositeTransactionId == null)
            .Where(t => !t.IsIntentionallyUnlinked)
            .Where(t => t.TransactionType == oppositeType)
            .Where(t => t.Symbol == sourceTransaction.Symbol)
            .Where(t => t.DateTime >= minTime && t.DateTime <= maxTime)
            .ToListAsync();

        var matches = candidates
            .Select(t =>
            {
                // Vergleichsbetrag je nach Typ
                var candidateAmount = t.TransactionType == TransactionType.Send
                    ? t.QuantityAfterFee
                    : t.Quantity;

                var amountDiff = Math.Abs(candidateAmount - compareAmount);
                var amountMatch = amountDiff <= compareAmount * amountTolerance;
                var timeDiff = Math.Abs((t.DateTime - sourceTransaction.DateTime).TotalMinutes);

                // Konfidenzberechnung
                var confidence = 0.0m;
                if (amountMatch)
                {
                    // Basiswert für Betragsübereinstimmung
                    confidence = 0.5m;

                    // Bonus für exakte Übereinstimmung
                    if (amountDiff == 0)
                        confidence += 0.2m;

                    // Bonus für zeitliche Nähe
                    if (timeDiff <= 5)
                        confidence += 0.2m;
                    else if (timeDiff <= 15)
                        confidence += 0.1m;

                    // Bonus wenn gleiche Adresse
                    if (!string.IsNullOrEmpty(t.Address) &&
                        !string.IsNullOrEmpty(sourceTransaction.Address) &&
                        t.Address == sourceTransaction.Address)
                        confidence += 0.1m;
                }

                return new
                {
                    t.Id,
                    DateTime = t.DateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Type = t.TransactionType.ToString(),
                    t.Symbol,
                    t.Quantity,
                    t.QuantityAfterFee,
                    t.Comment,
                    t.Address,
                    WalletName = t.Wallet.Name,
                    TimeDiffMinutes = Math.Round(timeDiff, 1),
                    AmountDiff = amountDiff,
                    Confidence = Math.Min(1.0m, confidence)
                };
            })
            .Where(m => m.Confidence > 0)
            .OrderByDescending(m => m.Confidence)
            .ThenBy(m => m.TimeDiffMinutes)
            .Take(10)
            .ToList();

        var result = new
        {
            sourceTransaction = new
            {
                sourceTransaction.Id,
                DateTime = sourceTransaction.DateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Type = sourceTransaction.TransactionType.ToString(),
                sourceTransaction.Symbol,
                sourceTransaction.Quantity,
                sourceTransaction.QuantityAfterFee,
                sourceTransaction.Comment,
                WalletName = sourceTransaction.Wallet.Name
            },
            matches
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
    }
}
