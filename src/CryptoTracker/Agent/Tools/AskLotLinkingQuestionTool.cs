using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using CryptoTracker.Shared;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool um den Benutzer eine Lot-Frage zu stellen.
/// </summary>
public sealed class AskLotLinkingQuestionTool : IAgentTool
{
    private const string FreeTextOptionLabel = "Andere Option (Freitext)";
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly ILotLinkingAgentContextAccessor _contextAccessor;

    public AskLotLinkingQuestionTool(
        CryptoTrackerDbContext dbContext,
        ILotLinkingAgentContextAccessor contextAccessor)
    {
        _dbContext = dbContext;
        _contextAccessor = contextAccessor;
    }

    public Delegate GetToolRunner() => AskLotQuestionAsync;
    public string GetToolName() => "ask_lot_user";
    public string GetToolDescription() => """
        Stellt dem Benutzer eine Lot-Frage.
        Parameter:
        - assignmentType: "transaction" oder "trade"
        - assignmentId: ID des Eintrags
        - question: Die Frage
        - options: Auswahlmöglichkeiten als Liste (mit '|' getrennt)
        - lotWalletId: Optional, WalletId aus dem die Lots gewählt werden sollen
        """;

    private async Task<string> AskLotQuestionAsync(
        [Description("assignmentType: transaction/trade")] string assignmentType,
        [Description("assignmentId")] int assignmentId,
        [Description("Frage")] string question,
        [Description("Optionen mit '|' getrennt")] string options,
        [Description("Optional: WalletId für Lot-Auswahl")] int? lotWalletId = null)
    {
        var context = _contextAccessor.Current;
        if (context == null)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Kein aktiver Session-Kontext" });
        }

        if (!context.AllowQuestions)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Fragen sind in dieser Session nicht erlaubt" });
        }

        var optionList = ParseOptions(options);
        if (!optionList.Contains(FreeTextOptionLabel, StringComparer.OrdinalIgnoreCase))
        {
            optionList.Add(FreeTextOptionLabel);
        }

        PendingLotAssignmentDTO? assignment = null;
        string? symbol = null;
        int? walletId = lotWalletId;
        DateTimeOffset? maxAcquisitionDate = null;

        if (assignmentType.Equals("transaction", StringComparison.OrdinalIgnoreCase))
        {
            var tx = await _dbContext.CryptoTransactions
                .Include(t => t.Wallet)
                .Include(t => t.OppositeWallet)
                .FirstOrDefaultAsync(t => t.Id == assignmentId);

            if (tx != null)
            {
                assignment = new PendingLotAssignmentDTO(
                    "Transaction",
                    tx.Id,
                    tx.DateTime,
                    tx.Symbol,
                    tx.QuantityAfterFee,
                    tx.Wallet.Name,
                    tx.WalletId,
                    "Receive",
                    tx.OppositeWallet?.Name,
                    tx.Comment)
                {
                    OppositeTransactionId = tx.OppositeTransactionId
                };

                symbol = tx.Symbol;
                walletId ??= tx.WalletId;
                maxAcquisitionDate = tx.DateTime;
            }
        }
        else if (assignmentType.Equals("trade", StringComparison.OrdinalIgnoreCase))
        {
            var trade = await _dbContext.CryptoTrades
                .Include(t => t.Wallet)
                .FirstOrDefaultAsync(t => t.Id == assignmentId);

            if (trade != null)
            {
                var quantity = trade.TradeType == TradeType.Buy ? trade.QuantityAfterFee : trade.Quantity;
                assignment = new PendingLotAssignmentDTO(
                    "Trade",
                    trade.Id,
                    trade.DateTime,
                    trade.Symbol,
                    quantity,
                    trade.Wallet.Name,
                    trade.WalletId,
                    trade.TradeType.ToString(),
                    null,
                    trade.Comment)
                {
                    OppositeTradeId = trade.OppositeTradeId,
                    OppositeSymbol = trade.OppositeSymbol
                };

                symbol = trade.Symbol;
                walletId ??= trade.WalletId;
                maxAcquisitionDate = trade.DateTime;
            }
        }

        var lotOptions = await LoadLotOptionsAsync(symbol, walletId, maxAcquisitionDate);

        var questionId = Guid.NewGuid().ToString("N");
        context.Session.CurrentQuestionId = questionId;
        context.Session.CurrentQuestion = question;
        context.Session.CurrentAssignment = assignment;
        context.Session.CurrentOptions = optionList;
        context.Session.CurrentLotOptions = lotOptions;

        await context.SendEventAsync(new LotLinkingEventDTO
        {
            EventType = "question",
            QuestionId = questionId,
            Message = question,
            Assignment = assignment,
            Options = optionList,
            LotOptions = lotOptions,
            ProcessedCount = context.Session.ProcessedCount,
            TotalCount = context.Session.TotalCount
        });

        return JsonSerializer.Serialize(new { success = true, questionId });
    }

    private async Task<IList<LotOptionDTO>> LoadLotOptionsAsync(
        string? symbol,
        int? walletId,
        DateTimeOffset? maxAcquisitionDate)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return new List<LotOptionDTO>();
        }

        var query = _dbContext.AssetLots
            .Include(l => l.CurrentWallet)
            .Where(l => l.Symbol == symbol.ToUpperInvariant())
            .Where(l => l.RemainingQuantity > 0);

        if (walletId.HasValue)
        {
            query = query.Where(l => l.CurrentWalletId == walletId.Value);
        }

        if (maxAcquisitionDate.HasValue)
        {
            query = query.Where(l => l.AcquisitionDate <= maxAcquisitionDate.Value);
        }

        var lots = await query
            .OrderBy(l => l.AcquisitionDate)
            .Take(200)
            .ToListAsync();

        var result = new List<LotOptionDTO>();
        foreach (var lot in lots)
        {
            var rootId = await GetRootLotIdAsync(lot);
            var rootLabel = rootId == lot.Id ? $"Root #{rootId}" : $"Root #{rootId} → Lot #{lot.Id}";
            var altLabel = lot.IsAltbestand ? "Altbestand" : "Neubestand";

            result.Add(new LotOptionDTO
            {
                LotId = lot.Id,
                DisplayText = $"{rootLabel} | {lot.RemainingQuantity:F8} {lot.Symbol} | {altLabel} | {lot.AcquisitionDate:yyyy-MM-dd} | {lot.CurrentWallet.Name}",
                AvailableQuantity = lot.RemainingQuantity,
                AcquisitionDate = lot.AcquisitionDate,
                AcquisitionPriceEur = lot.AcquisitionPriceEur,
                IsAltbestand = lot.IsAltbestand,
                WalletName = lot.CurrentWallet.Name
            });
        }

        return result;
    }

    private async Task<int> GetRootLotIdAsync(AssetLot lot)
    {
        var current = lot;
        while (current.ParentLotId.HasValue)
        {
            var parent = await _dbContext.AssetLots.FindAsync(current.ParentLotId.Value);
            if (parent == null)
            {
                break;
            }
            current = parent;
        }

        return current.Id;
    }

    private static List<string> ParseOptions(string options)
    {
        return options
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
