namespace CryptoTracker.Shared;

/// <summary>
/// Status des AI-Services
/// </summary>
public record ServiceStatusDTO
{
    public bool IsConfigured { get; init; }
    public string Message { get; init; } = "";
}

/// <summary>
/// Statistiken über Verknüpfungen
/// </summary>
public record LinkingStatisticsDTO
{
    public int TotalTransactions { get; init; }
    public int LinkedTransactions { get; init; }
    public int IntentionallyUnlinked { get; init; }
    public int UnlinkedTransactions { get; init; }
    public int ConfirmedLinks { get; init; }
    public int UnconfirmedLinks { get; init; }
    public Dictionary<string, int> UnlinkedBySymbol { get; init; } = new();
    
    /// <summary>
    /// Prozent verknüpfte Transaktionen
    /// </summary>
    public decimal LinkedPercent => TotalTransactions > 0 
        ? Math.Round((decimal)LinkedTransactions / TotalTransactions * 100, 1) 
        : 0;
}

/// <summary>
/// Ergebnis einer Agent-Session
/// </summary>
public record LinkingSessionResultDTO
{
    public string AgentResponse { get; init; } = "";
    public bool Success { get; init; }
    public IList<TransactionLinkProposalDTO>? Proposals { get; init; }
}

/// <summary>
/// Vorschlag für eine Verknüpfung
/// </summary>
public record TransactionLinkProposalDTO
{
    public int SendId { get; init; }
    public int ReceiveId { get; init; }
    public decimal Confidence { get; init; }
    public string Reason { get; init; } = "";
    public UnlinkedTransactionDTO? SendTransaction { get; init; }
    public UnlinkedTransactionDTO? ReceiveTransaction { get; init; }
}

/// <summary>
/// Ergebnis der automatischen Verknüpfung
/// </summary>
public record AutoLinkResultDTO
{
    public int LinkedCount { get; init; }
    public int MarkedUnlinkedCount { get; init; }
    public int RemainingUnlinkedCount { get; init; }
    public string Summary { get; init; } = "";
    public bool Success { get; init; }
}

/// <summary>
/// Unverknüpfte Transaktion
/// </summary>
public record UnlinkedTransactionDTO
{
    public int Id { get; init; }
    public DateTimeOffset DateTime { get; init; }
    public string Type { get; init; } = "";
    public string Symbol { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal QuantityAfterFee { get; init; }
    public string? Comment { get; init; }
    public string? Address { get; init; }
    public string WalletName { get; init; } = "";
    public string? TransactionId { get; init; }
    public string? Network { get; init; }
    
    /// <summary>
    /// Ob dies ein Send ist
    /// </summary>
    public bool IsSend => Type?.Equals("Send", StringComparison.OrdinalIgnoreCase) == true;
    
    /// <summary>
    /// Ob dies ein Receive ist
    /// </summary>
    public bool IsReceive => Type?.Equals("Receive", StringComparison.OrdinalIgnoreCase) == true;
}

/// <summary>
/// Ergebnis einer manuellen Verknüpfung
/// </summary>
public record LinkResultDTO
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
}

/// <summary>
/// Ergebnis der Bestätigung
/// </summary>
public record ConfirmResultDTO
{
    public int ConfirmedCount { get; init; }
}

/// <summary>
/// Ergebnis eines Resets
/// </summary>
public record ResetResultDTO
{
    public int ResetCount { get; init; }
    public int MetadataDeleted { get; init; }
}

/// <summary>
/// Chat-Nachricht für Agent-Konversation
/// </summary>
public record ChatMessageDTO
{
    public string Role { get; init; } = ""; // "user" oder "assistant"
    public string Content { get; init; } = "";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public bool IsLoading { get; init; }
}

/// <summary>
/// Hint für potentielle Verknüpfung (vorberechnet)
/// </summary>
public record LinkingHintDTO
{
    public int SendId { get; init; }
    public int ReceiveId { get; init; }
    public decimal ConfidenceScore { get; init; }
    public string Reason { get; init; } = "";
    public TimeSpan TimeDifference { get; init; }
    public decimal AmountDifference { get; init; }
}

/// <summary>
/// Context für den Agent mit allen Daten
/// </summary>
public record LinkingContextDTO
{
    public IList<UnlinkedTransactionDTO> UnlinkedTransactions { get; init; } = [];
    public IList<LinkingHintDTO> Hints { get; init; } = [];
    public IList<LearnedRuleDTO> LearnedRules { get; init; } = [];
    public LinkingStatisticsDTO Statistics { get; init; } = new();
}

/// <summary>
/// Gelernte Regel für zukünftige Entscheidungen
/// </summary>
public record LearnedRuleDTO
{
    public string Id { get; init; } = "";
    public string RuleType { get; init; } = ""; // "skip_pattern", "link_pattern", "wallet_alias"
    public string Pattern { get; init; } = "";
    public string Action { get; init; } = ""; // "mark_external", "link", "skip"
    public string Description { get; init; } = "";
    public int TimesApplied { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Live-Event vom Agent
/// </summary>
public record LinkingEventDTO
{
    public string EventType { get; init; } = ""; // "linked", "marked_external", "question", "progress", "error", "rule_learned"
    public string Message { get; init; } = "";
    public int? SendId { get; init; }
    public int? ReceiveId { get; init; }
    public UnlinkedTransactionDTO? Transaction { get; init; }
    public string? QuestionId { get; init; }
    public IList<string>? Options { get; init; }
    public int ProcessedCount { get; init; }
    public int TotalCount { get; init; }
}

/// <summary>
/// Antwort vom User auf Agent-Frage
/// </summary>
public record UserResponseDTO
{
    public string QuestionId { get; init; } = "";
    public string Response { get; init; } = "";
    public bool ShouldRemember { get; init; } = true;
    public string? Action { get; init; }
    public int? VirtualWalletId { get; init; }
    public string? VirtualWalletName { get; init; }
}

/// <summary>
/// Session-State für interaktives Linking
/// </summary>
public record InteractiveLinkingSessionDTO
{
    public string SessionId { get; init; } = "";
    public bool IsActive { get; init; }
    public int ProcessedCount { get; init; }
    public int TotalCount { get; init; }
    public int LinkedCount { get; init; }
    public int MarkedExternalCount { get; init; }
    public int SkippedCount { get; init; }
    public string? CurrentQuestionId { get; init; }
    public string? CurrentQuestion { get; init; }
    public UnlinkedTransactionDTO? CurrentTransaction { get; init; }
    public IList<string>? CurrentOptions { get; init; }
}

