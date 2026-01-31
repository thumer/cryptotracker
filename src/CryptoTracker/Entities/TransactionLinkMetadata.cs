namespace CryptoTracker.Entities;

/// <summary>
/// Metadaten zur Transaktionsverknüpfung.
/// Speichert, wie und warum eine Transaktion verknüpft wurde.
/// </summary>
public class TransactionLinkMetadata
{
    public int Id { get; set; }

    /// <summary>
    /// Die verknüpfte Transaktion (Send oder Receive)
    /// </summary>
    public int TransactionId { get; set; }
    public CryptoTransaction Transaction { get; set; } = null!;

    /// <summary>
    /// Art der Verknüpfung (Flags - können kombiniert werden)
    /// </summary>
    public TransactionLinkType LinkType { get; set; }

    /// <summary>
    /// Konfidenz der Verknüpfung (0.0 - 1.0)
    /// 1.0 = Sicher (z.B. manuelle Bestätigung)
    /// 0.0-0.5 = Unsicher, Benutzerbestätigung empfohlen
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Begründung für die Verknüpfung (vom LLM oder System generiert)
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Wurde vom Benutzer bestätigt?
    /// Unbestätigte Verknüpfungen können später überprüft werden.
    /// </summary>
    public bool IsConfirmed { get; set; }

    /// <summary>
    /// Zeitpunkt der Verknüpfung
    /// </summary>
    public DateTimeOffset LinkedAt { get; set; }

    /// <summary>
    /// Zeitpunkt der Benutzerbestätigung (falls bestätigt)
    /// </summary>
    public DateTimeOffset? ConfirmedAt { get; set; }
}
