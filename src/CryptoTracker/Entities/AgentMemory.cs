namespace CryptoTracker.Entities;

/// <summary>
/// Typ des Agent-Gedächtniseintrags
/// </summary>
public enum AgentMemoryType
{
    /// <summary>
    /// Kommentar-Muster zum Überspringen (z.B. "ETH 2.0 Staking Rewards" = externe Einnahme)
    /// </summary>
    SkipPattern,

    /// <summary>
    /// Bekannte Wallet-Zuordnung (z.B. "PC-Wallet Thomas PC" → Interner Transfer)
    /// </summary>
    WalletMapping,

    /// <summary>
    /// Bekannte Adress-Zuordnung (z.B. bestimmte Adresse gehört zu eigenem Wallet)
    /// </summary>
    AddressMapping,

    /// <summary>
    /// Benutzer-Entscheidung für ähnliche Fälle
    /// </summary>
    UserDecision,

    /// <summary>
    /// Erkanntes Importfehler-Muster (z.B. UTC vs. Lokalzeit)
    /// </summary>
    ImportErrorPattern
}

/// <summary>
/// Persistentes Gedächtnis für den Linking-Agent.
/// Speichert gelernte Regeln und Benutzerentscheidungen.
/// </summary>
public class AgentMemory
{
    public int Id { get; set; }

    /// <summary>
    /// Agent-Schlüssel (z.B. "transaction-linking", "lot-linking")
    /// </summary>
    public string AgentKey { get; set; } = string.Empty;

    /// <summary>
    /// Typ des Eintrags
    /// </summary>
    public AgentMemoryType MemoryType { get; set; }

    /// <summary>
    /// Schlüssel für den Eintrag (z.B. Kommentar-Pattern, Adresse, Wallet-Name)
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Wert/Payload (JSON oder einfacher String)
    /// Bei SkipPattern: "true" oder Begründung
    /// Bei WalletMapping: WalletId oder Wallet-Name
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Beschreibung/Begründung (menschenlesbar)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Erstellt am
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Wie oft wurde diese Regel angewendet?
    /// Hilft bei der Priorisierung und Aufräumen ungenutzter Regeln.
    /// </summary>
    public int UsageCount { get; set; }
}
