namespace CryptoTracker.Agent.Common;

/// <summary>
/// Konfiguration für Azure OpenAI.
/// Property-Namen entsprechen den Environment-Variablen / secrets.json Keys.
/// </summary>
public class OpenAISettings
{
    /// <summary>
    /// Azure OpenAI Endpoint URL
    /// </summary>
    public string OpenAiEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Hauptmodell für komplexe Agent-Konversationen (z.B. gpt-5.2)
    /// </summary>
    public string OpenAiDeploymentName { get; set; } = "gpt-5.2";

    /// <summary>
    /// Schnelles Modell für Batch-Klassifizierung (z.B. gpt-5-nano)
    /// </summary>
    public string OpenAiFastDeploymentName { get; set; } = "gpt-5-nano";

    /// <summary>
    /// Embedding-Modell für Ähnlichkeitssuche
    /// </summary>
    public string OpenAiEmbeddingDeploymentName { get; set; } = "text-embedding-3-large";

    /// <summary>
    /// Dimensionen der Embedding-Vektoren
    /// </summary>
    public int OpenAiEmbeddingVectorDimensions { get; set; } = 1536;

    /// <summary>
    /// API-Key (optional, falls nicht DefaultAzureCredential verwendet wird)
    /// </summary>
    public string? OpenAiKey { get; set; }

    /// <summary>
    /// Prüft ob die OpenAI-Konfiguration vollständig ist
    /// </summary>
    public bool IsConfigured => !string.IsNullOrEmpty(OpenAiEndpoint) && !string.IsNullOrEmpty(OpenAiDeploymentName);
}
