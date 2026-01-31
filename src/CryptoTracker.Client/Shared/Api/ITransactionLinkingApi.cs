namespace CryptoTracker.Shared;

/// <summary>
/// API für KI-gestützte Transaktionsverknüpfung
/// </summary>
public interface ITransactionLinkingApi
{
    /// <summary>
    /// Prüft ob der AI-Service konfiguriert ist
    /// </summary>
    Task<ServiceStatusDTO> GetStatusAsync();

    /// <summary>
    /// Gibt Statistiken über Verknüpfungen zurück
    /// </summary>
    Task<LinkingStatisticsDTO> GetStatisticsAsync();

    /// <summary>
    /// Startet eine neue Agent-Session
    /// </summary>
    Task<LinkingSessionResultDTO> StartSessionAsync();

    /// <summary>
    /// Sendet eine Nachricht an den Agent
    /// </summary>
    Task<LinkingSessionResultDTO> SendMessageAsync(string message);

    /// <summary>
    /// Führt automatische Verknüpfung durch
    /// </summary>
    Task<AutoLinkResultDTO> RunAutoLinkAsync();

    /// <summary>
    /// Gibt unverknüpfte Transaktionen zurück
    /// </summary>
    Task<IList<UnlinkedTransactionDTO>> GetUnlinkedAsync(string? type = null, string? symbol = null, int limit = 100, int offset = 0);

    /// <summary>
    /// Verknüpft zwei Transaktionen manuell
    /// </summary>
    Task<LinkResultDTO> ManualLinkAsync(int sendId, int receiveId, string? reason = null);

    /// <summary>
    /// Bestätigt vorgeschlagene Verknüpfungen
    /// </summary>
    Task<ConfirmResultDTO> ConfirmLinksAsync(IList<int> transactionIds);

    /// <summary>
    /// Setzt alle Verknüpfungen zurück
    /// </summary>
    Task<ResetResultDTO> ResetLinksAsync(bool keepManualLinks = true, bool resetIntentionallyUnlinked = false);

    /// <summary>
    /// Markiert eine Transaktion als absichtlich unverknüpft (externe Einnahme)
    /// </summary>
    Task<LinkResultDTO> MarkAsIntentionallyUnlinkedAsync(int transactionId, string reason);
}
