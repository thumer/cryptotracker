namespace CryptoTracker.Entities;

/// <summary>
/// Flags für die Art der Transaktionsverknüpfung.
/// Mehrere Typen können kombiniert werden (z.B. TimeAndAmount | AIAssisted).
/// </summary>
[Flags]
public enum TransactionLinkType
{
    None = 0,

    /// <summary>
    /// Exakte Zeit- und Betrags-Übereinstimmung (alte ProcessTransactionPairs-Logik)
    /// </summary>
    TimeAndAmount = 1,

    /// <summary>
    /// Via LLM/KI-Analyse verknüpft
    /// </summary>
    AIAssisted = 2,

    /// <summary>
    /// Direkte Verknüpfung (gleiche TxId, Adresse)
    /// </summary>
    Direct = 4,

    /// <summary>
    /// Indirekte Verknüpfung (Kommentar-Analyse, Wallet-Name)
    /// </summary>
    Indirect = 8,

    /// <summary>
    /// Automatisch vom System verknüpft (ohne Benutzerinteraktion)
    /// </summary>
    Automatic = 16,

    /// <summary>
    /// Manuell vom Benutzer verknüpft
    /// </summary>
    Manual = 32,

    /// <summary>
    /// Bewusst ohne Gegenstück gelassen (z.B. Staking Rewards, Airdrops)
    /// </summary>
    IntentionallyUnlinked = 64
}
