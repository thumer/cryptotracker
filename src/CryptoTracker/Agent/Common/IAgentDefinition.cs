namespace CryptoTracker.Agent.Common;

/// <summary>
/// Metadaten für einen Agent
/// </summary>
public record AgentMetadata(
    string Key,
    string DisplayName,
    string Description
);

/// <summary>
/// Prompt-Definition für einen Agent
/// </summary>
public record AgentPromptDefinition(
    string SystemPrompt
);

/// <summary>
/// Interface für Agent-Definitionen
/// </summary>
public interface IAgentDefinition
{
    /// <summary>
    /// Metadaten des Agents (Key, Name, Beschreibung)
    /// </summary>
    AgentMetadata Metadata { get; }

    /// <summary>
    /// Prompt-Definition (System-Prompt)
    /// </summary>
    AgentPromptDefinition PromptDefinition { get; }

    /// <summary>
    /// Tools die dem Agent zur Verfügung stehen
    /// </summary>
    IReadOnlyList<IAgentTool> Tools { get; }
}
