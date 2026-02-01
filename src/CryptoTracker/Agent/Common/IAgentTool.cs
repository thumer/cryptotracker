using System.Text.Json.Serialization;

namespace CryptoTracker.Agent.Common;

/// <summary>
/// Interface für Agent-Tools (Function Calling)
/// </summary>
public interface IAgentTool
{
    /// <summary>
    /// Gibt den Delegate zurück, der vom Agent aufgerufen wird
    /// </summary>
    Delegate GetToolRunner();

    /// <summary>
    /// Name des Tools (für Function Calling, snake_case)
    /// </summary>
    string GetToolName();

    /// <summary>
    /// Beschreibung des Tools (für LLM-Kontext)
    /// </summary>
    string GetToolDescription();

    /// <summary>
    /// Optional: JsonSerializerContext für komplexe Typen
    /// </summary>
    JsonSerializerContext? GetJsonSerializerContext() => null;
}
