using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CryptoTracker.Agent.Common;

/// <summary>
/// Builder für AI-Agents mit Multi-Model-Unterstützung
/// </summary>
public class AILinkingAgentBuilder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<OpenAISettings> _settings;

    public AILinkingAgentBuilder(IOptions<OpenAISettings> settings, IServiceProvider serviceProvider)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Erstellt einen Agent mit dem Hauptmodell (z.B. gpt-5.2)
    /// Für komplexe Konversationen und Steuerberatung
    /// </summary>
    public AIAgent BuildAgent(string agentKey)
    {
        return BuildAgentWithModel(agentKey, _settings.Value.OpenAiDeploymentName);
    }

    /// <summary>
    /// Erstellt einen Agent mit dem Fast-Modell (z.B. gpt-5-nano)
    /// Für schnelle Batch-Klassifizierung und einfache Entscheidungen
    /// </summary>
    public AIAgent BuildFastAgent(string agentKey)
    {
        return BuildAgentWithModel(agentKey, _settings.Value.OpenAiFastDeploymentName);
    }

    private AIAgent BuildAgentWithModel(string agentKey, string deploymentName)
    {
        var scope = _serviceProvider.CreateScope();
        var openAiClient = scope.ServiceProvider.GetRequiredService<AzureOpenAIClient>();
        var definitions = scope.ServiceProvider.GetServices<IAgentDefinition>();

        var definition = definitions.FirstOrDefault(d => d.Metadata.Key == agentKey)
            ?? throw new ArgumentException($"Agent '{agentKey}' nicht gefunden");

        var chatClient = openAiClient
            .GetChatClient(deploymentName)
            .AsIChatClient();

        var tools = definition.Tools
            .Select(t => AIFunctionFactory.Create(
                t.GetToolRunner(),
                t.GetToolName(),
                t.GetToolDescription(),
                t.GetJsonSerializerContext() != null
                    ? new JsonSerializerOptions
                    {
                        TypeInfoResolver = JsonTypeInfoResolver.Combine(
                            t.GetJsonSerializerContext()!.Options.TypeInfoResolver,
                            new DefaultJsonTypeInfoResolver())
                    }
                    : null))
            .ToArray();

        var chatAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = definition.Metadata.Key,
            Description = definition.Metadata.DisplayName,
            ChatOptions = new ChatOptions
            {
                Instructions = definition.PromptDefinition.SystemPrompt,
                Tools = tools
            }
        });

        return chatAgent.AsBuilder().Build();
    }

    /// <summary>
    /// Gibt einen Embedding-Client für Ähnlichkeitssuche zurück
    /// </summary>
    public EmbeddingClient GetEmbeddingClient()
    {
        var scope = _serviceProvider.CreateScope();
        var openAiClient = scope.ServiceProvider.GetRequiredService<AzureOpenAIClient>();
        return openAiClient.GetEmbeddingClient(_settings.Value.OpenAiEmbeddingDeploymentName);
    }

    /// <summary>
    /// Prüft ob die OpenAI-Konfiguration vorhanden ist
    /// </summary>
    public bool IsConfigured()
    {
        return _settings.Value.IsConfigured;
    }
}
