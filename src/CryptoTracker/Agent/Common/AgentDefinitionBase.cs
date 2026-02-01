namespace CryptoTracker.Agent.Common;

/// <summary>
/// Basisklasse für Agent-Definitionen
/// </summary>
public abstract class AgentDefinitionBase : IAgentDefinition
{
    public AgentMetadata Metadata { get; }
    public AgentPromptDefinition PromptDefinition { get; }
    public IReadOnlyList<IAgentTool> Tools { get; }

    protected AgentDefinitionBase(
        AgentMetadata metadata,
        AgentPromptDefinition promptDefinition,
        IReadOnlyList<IAgentTool> tools)
    {
        Metadata = metadata;
        PromptDefinition = promptDefinition;
        Tools = tools;
    }
}
