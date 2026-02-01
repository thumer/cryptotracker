using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using CryptoTracker.Shared;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool um den Benutzer eine Frage zu stellen (Transaction Linking).
/// </summary>
public sealed class AskLinkingQuestionTool : IAgentTool
{
    private const string FreeTextOptionLabel = "Andere Option (Freitext)";
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly ILinkingAgentContextAccessor _contextAccessor;

    public AskLinkingQuestionTool(
        CryptoTrackerDbContext dbContext,
        ILinkingAgentContextAccessor contextAccessor)
    {
        _dbContext = dbContext;
        _contextAccessor = contextAccessor;
    }

    public Delegate GetToolRunner() => AskUserAsync;
    public string GetToolName() => "ask_user";
    public string GetToolDescription() => """
        Stellt dem Benutzer eine Frage. Verwende dieses Tool wenn du unsicher bist.
        Parameter:
        - transactionId: Optional, ID der betroffenen Transaktion
        - question: Die Frage an den Benutzer
        - options: Auswahlmöglichkeiten als Liste (mit '|' getrennt), z.B. "Option A | Option B | Überspringen"
        
        Hinweis: Die Freitext-Option wird automatisch hinzugefügt.
        """;

    private async Task<string> AskUserAsync(
        [Description("Optional: Transaktions-ID")] int? transactionId,
        [Description("Frage an den Benutzer")] string question,
        [Description("Auswahlmöglichkeiten, getrennt mit '|'")] string options)
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

        var questionId = Guid.NewGuid().ToString("N");
        UnlinkedTransactionDTO? transaction = null;

        if (transactionId.HasValue)
        {
            transaction = await _dbContext.CryptoTransactions
                .Include(t => t.Wallet)
                .Where(t => t.Id == transactionId.Value)
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
                .FirstOrDefaultAsync();
        }

        context.Session.CurrentQuestionId = questionId;
        context.Session.CurrentQuestion = question;
        context.Session.CurrentTransaction = transaction;
        context.Session.CurrentOptions = optionList;

        await context.SendEventAsync(new LinkingEventDTO
        {
            EventType = "question",
            QuestionId = questionId,
            Message = question,
            Transaction = transaction,
            Options = optionList,
            ProcessedCount = context.Session.ProcessedCount,
            TotalCount = context.Session.TotalCount
        });

        return JsonSerializer.Serialize(new { success = true, questionId });
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
