# Plan: KI-gestützte Transaktions- und Lot-Verknüpfung

## Zusammenfassung

Dieses Dokument beschreibt die Implementierung eines KI-gestützten Assistenten-Systems für:
1. **Transaktionsverknüpfung**: Intelligente Paarung von Send/Receive-Transaktionen
2. **Lot-Verknüpfung**: Verkettung von Asset-Lots für vollständige Steuerketten

Das System nutzt **Azure OpenAI** mit dem **Microsoft Agent Framework** für intelligente Entscheidungen und bietet eine interaktive Wizard-UI für Benutzerbestätigungen.

---

## 1. Architektur-Übersicht

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Blazor UI                                       │
│  ┌─────────────────────┐  ┌─────────────────────┐  ┌─────────────────────┐  │
│  │ TransaktionsPairing │  │   LotVerknüpfung    │  │   Wizard-Ansicht    │  │
│  │      .razor         │  │      .razor         │  │   (Interaktiv)      │  │
│  └──────────┬──────────┘  └──────────┬──────────┘  └──────────┬──────────┘  │
└─────────────┼───────────────────────┼───────────────────────┼───────────────┘
              │                       │                       │
              ▼                       ▼                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           API Controller Layer                               │
│  ┌─────────────────────────────┐  ┌─────────────────────────────────────┐   │
│  │ TransactionLinkingController│  │      LotLinkingController           │   │
│  └──────────────┬──────────────┘  └──────────────┬──────────────────────┘   │
└─────────────────┼────────────────────────────────┼──────────────────────────┘
                  │                                │
                  ▼                                ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                            Agent Services                                    │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │                    AILinkingAgentBuilder                              │   │
│  │  ┌────────────────────────┐  ┌────────────────────────────────────┐  │   │
│  │  │TransactionLinkingAgent │  │      LotLinkingAgent               │  │   │
│  │  │  - Tools               │  │  - Tools                           │  │   │
│  │  │  - Prompts             │  │  - Prompts                         │  │   │
│  │  │  - Memory (DB)         │  │  - Memory (DB)                     │  │   │
│  │  └────────────────────────┘  └────────────────────────────────────┘  │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
                  │                                │
                  ▼                                ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Azure OpenAI                                    │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────────┐  │
│  │    gpt-5.2      │  │   gpt-5-nano    │  │  text-embedding-3-large    │  │
│  │  (Hauptmodell)  │  │     (Fast)      │  │      (Embeddings)          │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. NuGet-Pakete

Basierend auf dem StockManager-Projekt werden folgende Pakete benötigt:

```xml
<!-- Azure OpenAI SDK -->
<PackageReference Include="Azure.AI.OpenAI" Version="2.8.0-beta.1" />
<PackageReference Include="Azure.Identity" Version="1.17.1" />

<!-- Microsoft Agent Framework -->
<PackageReference Include="Microsoft.Agents.AI" Version="1.0.0-preview.260128.1" />
<PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.0.0-preview.260128.1" />
<PackageReference Include="Microsoft.Agents.AI.Hosting.OpenAI" Version="1.0.0-alpha.260128.1" />

<!-- Microsoft.Extensions.AI (für AIFunctionFactory) -->
<PackageReference Include="Microsoft.Extensions.AI" Version="10.2.0-preview.1.26063.2" />
<PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="10.2.0-preview.1.26063.2" />
```

> **Hinweis**: Immer die neuesten Preview-Versionen von NuGet.org verwenden! Oben sind die Versionen aus StockManager (Stand Januar 2026).

---

## 3. Datenmodell-Erweiterungen

### 3.1 Neue Entity: `TransactionLinkMetadata`

Speichert Metadaten zur Verknüpfung von Transaktionen.

```csharp
namespace CryptoTracker.Entities;

/// <summary>
/// Flags für die Art der Transaktionsverknüpfung
/// </summary>
[Flags]
public enum TransactionLinkType
{
    None = 0,
    
    /// <summary>Exakte Zeit & Betrag Übereinstimmung</summary>
    TimeAndAmount = 1,
    
    /// <summary>Via LLM/KI-Analyse verknüpft</summary>
    AIAssisted = 2,
    
    /// <summary>Direkte Verknüpfung (gleiche TxId, Adresse)</summary>
    Direct = 4,
    
    /// <summary>Indirekte Verknüpfung (Kommentar, Wallet-Name)</summary>
    Indirect = 8,
    
    /// <summary>Automatisch vom System verknüpft</summary>
    Automatic = 16,
    
    /// <summary>Manuell vom Benutzer verknüpft</summary>
    Manual = 32,
    
    /// <summary>Bewusst ohne Gegenstück gelassen (z.B. Staking Rewards)</summary>
    IntentionallyUnlinked = 64
}

/// <summary>
/// Metadaten zur Transaktionsverknüpfung
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
    /// Art der Verknüpfung (Flags)
    /// </summary>
    public TransactionLinkType LinkType { get; set; }
    
    /// <summary>
    /// Konfidenz der Verknüpfung (0.0 - 1.0)
    /// </summary>
    public decimal Confidence { get; set; }
    
    /// <summary>
    /// Begründung für die Verknüpfung (vom LLM oder System)
    /// </summary>
    public string? Reason { get; set; }
    
    /// <summary>
    /// Wurde vom Benutzer bestätigt?
    /// </summary>
    public bool IsConfirmed { get; set; }
    
    /// <summary>
    /// Zeitpunkt der Verknüpfung
    /// </summary>
    public DateTimeOffset LinkedAt { get; set; }
    
    /// <summary>
    /// Zeitpunkt der Benutzerbestätigung
    /// </summary>
    public DateTimeOffset? ConfirmedAt { get; set; }
}
```

### 3.2 Neue Entity: `AgentMemory`

Persistenter Speicher für Agent-Entscheidungen und Regeln.

```csharp
namespace CryptoTracker.Entities;

/// <summary>
/// Typ des Agent-Gedächtniseintrags
/// </summary>
public enum AgentMemoryType
{
    /// <summary>Kommentar-Muster zum Überspringen (z.B. "ETH 2.0 Staking Rewards")</summary>
    SkipPattern,
    
    /// <summary>Bekannte Wallet-Zuordnung (z.B. "PC-Wallet Thomas PC" → WalletId)</summary>
    WalletMapping,
    
    /// <summary>Bekannte Adress-Zuordnung</summary>
    AddressMapping,
    
    /// <summary>Benutzer-Entscheidung für ähnliche Fälle</summary>
    UserDecision,
    
    /// <summary>Erkanntes Importfehler-Muster</summary>
    ImportErrorPattern
}

/// <summary>
/// Persistentes Gedächtnis für den Linking-Agent
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
    /// Schlüssel für den Eintrag (z.B. Kommentar-Pattern, Adresse)
    /// </summary>
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// Wert/Payload (JSON oder einfacher String)
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// Beschreibung/Begründung
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Erstellt am
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
    
    /// <summary>
    /// Wie oft wurde diese Regel angewendet?
    /// </summary>
    public int UsageCount { get; set; }
}
```

### 3.3 Erweiterung: `CryptoTransaction`

```csharp
// Neue Properties in CryptoTransaction.cs

/// <summary>
/// Metadaten zur Verknüpfung
/// </summary>
public TransactionLinkMetadata? LinkMetadata { get; set; }

/// <summary>
/// Wurde bewusst ohne Gegenstück gelassen?
/// </summary>
public bool IsIntentionallyUnlinked { get; set; }
```

---

## 4. Agent-Implementierung

### 4.1 Verzeichnisstruktur

```
src/CryptoTracker/
├── Agent/
│   ├── Common/
│   │   ├── IAgentDefinition.cs
│   │   ├── IAgentTool.cs
│   │   ├── AgentDefinitionBase.cs
│   │   └── AILinkingAgentBuilder.cs
│   ├── Definitions/
│   │   ├── TransactionLinkingAgentDefinition.cs
│   │   └── LotLinkingAgentDefinition.cs
│   ├── Tools/
│   │   ├── GetUnlinkedTransactionsTool.cs
│   │   ├── GetTransactionDetailsTool.cs
│   │   ├── LinkTransactionsTool.cs
│   │   ├── MarkAsIntentionallyUnlinkedTool.cs
│   │   ├── SaveAgentMemoryTool.cs
│   │   ├── GetAgentMemoryTool.cs
│   │   ├── GetUnlinkedLotsTool.cs
│   │   ├── GetLotDetailsTool.cs
│   │   └── LinkLotsTool.cs
│   └── Services/
│       ├── TransactionLinkingService.cs
│       └── LotLinkingService.cs
```

### 4.2 Interface: `IAgentTool`

```csharp
namespace CryptoTracker.Agent.Common;

public interface IAgentTool
{
    /// <summary>
    /// Gibt den Delegate zurück, der vom Agent aufgerufen wird
    /// </summary>
    Delegate GetToolRunner();
    
    /// <summary>
    /// Name des Tools (für Function Calling)
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
```

### 4.3 Interface: `IAgentDefinition`

```csharp
namespace CryptoTracker.Agent.Common;

public record AgentMetadata(
    string Key,
    string DisplayName,
    string Description
);

public record AgentPromptDefinition(
    string SystemPrompt
);

public interface IAgentDefinition
{
    AgentMetadata Metadata { get; }
    AgentPromptDefinition PromptDefinition { get; }
    IReadOnlyList<IAgentTool> Tools { get; }
}
```

### 4.4 Transaction Linking Agent Definition

```csharp
namespace CryptoTracker.Agent.Definitions;

public sealed class TransactionLinkingAgentDefinition : AgentDefinitionBase
{
    public const string KEY = "transaction-linking";
    
    private const string SystemPrompt = """
ROLLE
Du bist ein Experte für Kryptowährungs-Transaktionsanalyse. Deine Aufgabe ist es, 
Send- und Receive-Transaktionen intelligent zu verknüpfen.

KONTEXT
- Transaktionen zwischen eigenen Wallets haben oft leicht unterschiedliche Zeiten 
  (Blockchain-Bestätigungszeit)
- Der Betrag nach Gebühren (QuantityAfterFee) beim Send sollte dem Receive entsprechen
- Kommentare können Hinweise auf die Herkunft geben

REGELN FÜR VERKNÜPFUNGEN
1. **Externe Einnahmen (NICHT verknüpfen)**:
   - "Staking Rewards", "ETH 2.0 Staking Rewards" → Externe Einnahme, kein Gegenstück
   - "Airdrop", "Bonus", "Referral" → Externe Einnahme
   - "Mining", "Lending Interest" → Externe Einnahme
   - "div. Käufe", "Kauf" → Kommt von Fiat-Kauf, kein Transfer-Gegenstück

2. **Interne Transfers (VERKNÜPFEN)**:
   - Kommentare wie "PC-Wallet", "Ledger", Wallet-Namen → Interner Transfer
   - Gleiche Adresse in verschiedenen Wallets → Wahrscheinlich verknüpft
   - Zeit innerhalb von ~30 Minuten + gleicher Betrag → Hohe Wahrscheinlichkeit

3. **Fehlende Gegenstücke**:
   - Wenn ein Send keinen passenden Receive hat, könnte das Wallet nicht importiert sein
   - Wenn ein Receive von einer bekannten eigenen Adresse kommt, aber kein Send existiert,
     schlage vor, dass das Quell-Wallet fehlt

IMPORTFEHLER ERKENNEN
- UTC vs. Lokalzeit-Differenzen (z.B. +1h, +2h Unterschied)
- Wenn Zeit um genau 1-2 Stunden abweicht, aber Betrag exakt passt → Zeitzone-Problem

TOOLS
- Verwende `get_unlinked_transactions` um unverknüpfte Transaktionen zu laden
- Verwende `get_transaction_details` für Details zu einer Transaktion
- Verwende `link_transactions` um zwei Transaktionen zu verknüpfen
- Verwende `mark_intentionally_unlinked` für Transaktionen ohne Gegenstück
- Verwende `save_memory` um Regeln zu speichern (z.B. "Überspringe alle Staking Rewards")
- Verwende `get_memory` um gespeicherte Regeln abzurufen

WORKFLOW
1. Lade zunächst gespeicherte Regeln (get_memory)
2. Lade unverknüpfte Transaktionen blockweise (get_unlinked_transactions mit limit/offset)
3. Analysiere jede Transaktion:
   - Prüfe ob bekannte Skip-Patterns zutreffen
   - Suche nach passenden Gegenstücken
   - Bei Unsicherheit: Frage den Benutzer
4. Speichere neue Regeln wenn der Benutzer eine wiederkehrende Entscheidung trifft

OUTPUT
Antworte immer auf Deutsch. Erkläre deine Entscheidungen kurz und prägnant.
Bei Rückfragen an den Benutzer, formuliere klare Ja/Nein-Fragen oder Multiple-Choice.
""";

    public TransactionLinkingAgentDefinition(
        GetUnlinkedTransactionsTool getUnlinkedTool,
        GetTransactionDetailsTool getDetailsTool,
        LinkTransactionsTool linkTool,
        MarkAsIntentionallyUnlinkedTool markUnlinkedTool,
        SaveAgentMemoryTool saveMemoryTool,
        GetAgentMemoryTool getMemoryTool)
        : base(
            new AgentMetadata(KEY, "Transaktions-Verknüpfungs-Assistent", 
                "Verknüpft Send/Receive-Transaktionen intelligent"),
            new AgentPromptDefinition(SystemPrompt),
            [getUnlinkedTool, getDetailsTool, linkTool, markUnlinkedTool, saveMemoryTool, getMemoryTool])
    {
    }
}
```

### 4.5 Beispiel-Tool: `GetUnlinkedTransactionsTool`

```csharp
namespace CryptoTracker.Agent.Tools;

public sealed class GetUnlinkedTransactionsTool : IAgentTool
{
    private readonly CryptoTrackerDbContext _dbContext;
    
    public GetUnlinkedTransactionsTool(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public Delegate GetToolRunner() => GetUnlinkedTransactionsAsync;
    public string GetToolName() => "get_unlinked_transactions";
    public string GetToolDescription() => """
        Lädt unverknüpfte Transaktionen. 
        Parameter: 
        - type: "send", "receive" oder "all"
        - symbol: Optional, z.B. "ETH", "BTC"
        - limit: Max. Anzahl (default 50)
        - offset: Für Paginierung
        - includeIntentionallyUnlinked: false = nur wirklich unverknüpfte
        Gibt JSON-Array mit Id, DateTime, Symbol, Quantity, QuantityAfterFee, Comment, Address, WalletName zurück.
        """;
    
    private async Task<string> GetUnlinkedTransactionsAsync(
        [Description("Filter: 'send', 'receive' oder 'all'")] string type = "all",
        [Description("Symbol-Filter, z.B. 'ETH'")] string? symbol = null,
        [Description("Max. Anzahl Ergebnisse")] int limit = 50,
        [Description("Offset für Paginierung")] int offset = 0,
        [Description("Auch bewusst unverknüpfte einschließen")] bool includeIntentionallyUnlinked = false)
    {
        var query = _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Where(t => t.OppositeTransactionId == null);
        
        if (!includeIntentionallyUnlinked)
            query = query.Where(t => !t.IsIntentionallyUnlinked);
        
        if (type == "send")
            query = query.Where(t => t.TransactionType == TransactionType.Send);
        else if (type == "receive")
            query = query.Where(t => t.TransactionType == TransactionType.Receive);
        
        if (!string.IsNullOrEmpty(symbol))
            query = query.Where(t => t.Symbol == symbol);
        
        var transactions = await query
            .OrderBy(t => t.DateTime)
            .Skip(offset)
            .Take(limit)
            .Select(t => new
            {
                t.Id,
                DateTime = t.DateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Type = t.TransactionType.ToString(),
                t.Symbol,
                t.Quantity,
                t.QuantityAfterFee,
                t.Comment,
                t.Address,
                t.TransactionId,
                WalletName = t.Wallet.Name
            })
            .ToListAsync();
        
        return JsonSerializer.Serialize(transactions);
    }
}
```

### 4.6 Beispiel-Tool: `LinkTransactionsTool`

```csharp
namespace CryptoTracker.Agent.Tools;

public sealed class LinkTransactionsTool : IAgentTool
{
    private readonly CryptoTrackerDbContext _dbContext;
    
    public LinkTransactionsTool(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public Delegate GetToolRunner() => LinkTransactionsAsync;
    public string GetToolName() => "link_transactions";
    public string GetToolDescription() => """
        Verknüpft zwei Transaktionen miteinander (Send mit Receive).
        Parameter:
        - sendTransactionId: ID der Send-Transaktion
        - receiveTransactionId: ID der Receive-Transaktion
        - linkType: Flags wie die Verknüpfung zustande kam (TimeAndAmount=1, AIAssisted=2, Direct=4, Indirect=8, Automatic=16)
        - confidence: Konfidenz 0.0-1.0
        - reason: Begründung für die Verknüpfung
        Gibt Erfolg/Fehler-Status zurück.
        """;
    
    private async Task<string> LinkTransactionsAsync(
        [Description("ID der Send-Transaktion")] int sendTransactionId,
        [Description("ID der Receive-Transaktion")] int receiveTransactionId,
        [Description("Link-Typ Flags (1=TimeAndAmount, 2=AIAssisted, 4=Direct, 8=Indirect, 16=Automatic)")] int linkType,
        [Description("Konfidenz 0.0-1.0")] decimal confidence,
        [Description("Begründung")] string reason)
    {
        var send = await _dbContext.CryptoTransactions.FindAsync(sendTransactionId);
        var receive = await _dbContext.CryptoTransactions.FindAsync(receiveTransactionId);
        
        if (send == null || receive == null)
            return JsonSerializer.Serialize(new { success = false, error = "Transaktion nicht gefunden" });
        
        if (send.TransactionType != TransactionType.Send)
            return JsonSerializer.Serialize(new { success = false, error = "Erste Transaktion muss Send sein" });
        
        if (receive.TransactionType != TransactionType.Receive)
            return JsonSerializer.Serialize(new { success = false, error = "Zweite Transaktion muss Receive sein" });
        
        // Verknüpfung erstellen
        send.OppositeTransactionId = receive.Id;
        send.OppositeWalletId = receive.WalletId;
        receive.OppositeTransactionId = send.Id;
        receive.OppositeWalletId = send.WalletId;
        
        // Metadaten speichern
        var metadata = new TransactionLinkMetadata
        {
            TransactionId = send.Id,
            LinkType = (TransactionLinkType)linkType,
            Confidence = confidence,
            Reason = reason,
            LinkedAt = DateTimeOffset.UtcNow,
            IsConfirmed = false // Muss noch vom Benutzer bestätigt werden
        };
        _dbContext.TransactionLinkMetadata.Add(metadata);
        
        await _dbContext.SaveChangesAsync();
        
        return JsonSerializer.Serialize(new { 
            success = true, 
            message = $"Transaktionen {sendTransactionId} und {receiveTransactionId} verknüpft" 
        });
    }
}
```

### 4.7 `AILinkingAgentBuilder`

```csharp
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
        _settings = settings;
        _serviceProvider = serviceProvider;
    }
    
    /// <summary>
    /// Erstellt einen Agent mit dem Hauptmodell (gpt-5.2)
    /// Für komplexe Konversationen und Steuerberatung
    /// </summary>
    public AIAgent BuildAgent(string agentKey)
    {
        return BuildAgentWithModel(agentKey, _settings.Value.DeploymentName);
    }
    
    /// <summary>
    /// Erstellt einen Agent mit dem Fast-Modell (gpt-5-nano)
    /// Für schnelle Batch-Klassifizierung und einfache Entscheidungen
    /// </summary>
    public AIAgent BuildFastAgent(string agentKey)
    {
        return BuildAgentWithModel(agentKey, _settings.Value.FastDeploymentName);
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
                t.GetJsonSerializerContext()?.Options))
            .ToArray();
        
        return chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = definition.Metadata.Key,
            Description = definition.Metadata.DisplayName,
            ChatOptions = new ChatOptions
            {
                Instructions = definition.PromptDefinition.SystemPrompt,
                Tools = tools
            }
        });
    }
    
    /// <summary>
    /// Gibt einen Embedding-Client für Ähnlichkeitssuche zurück
    /// </summary>
    public EmbeddingClient GetEmbeddingClient()
    {
        var scope = _serviceProvider.CreateScope();
        var openAiClient = scope.ServiceProvider.GetRequiredService<AzureOpenAIClient>();
        return openAiClient.GetEmbeddingClient(_settings.Value.EmbeddingDeploymentName);
    }
}
```

### 4.8 Beispiel: Fast-Agent für Batch-Klassifizierung

```csharp
/// <summary>
/// Nutzt gpt-5-nano für schnelle Klassifizierung von Transaktionen
/// </summary>
public class TransactionClassificationService
{
    private readonly AILinkingAgentBuilder _agentBuilder;
    
    public async Task<List<TransactionClassification>> ClassifyBatchAsync(
        List<CryptoTransaction> transactions,
        CancellationToken ct = default)
    {
        // Fast-Agent für schnelle Batch-Verarbeitung
        var fastAgent = _agentBuilder.BuildFastAgent("transaction-classifier");
        
        var results = new List<TransactionClassification>();
        
        // Batch von 10 Transaktionen pro Anfrage
        foreach (var batch in transactions.Chunk(10))
        {
            var prompt = $"""
                Klassifiziere diese Transaktionen schnell:
                {string.Join("\n", batch.Select(t => 
                    $"- ID:{t.Id}, Typ:{t.TransactionType}, Comment:'{t.Comment}'"))}
                
                Antworte nur mit JSON: [{"id": 1, "type": "staking|transfer|external|unknown"}]
                """;
            
            var result = await fastAgent.RunAsync(prompt, cancellationToken: ct);
            // Parse result...
        }
        
        return results;
    }
}
```

---

## 5. Service-Schicht

### 5.1 `TransactionLinkingService`

```csharp
namespace CryptoTracker.Agent.Services;

public class TransactionLinkingService
{
    private readonly AILinkingAgentBuilder _agentBuilder;
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly ILogger<TransactionLinkingService> _logger;
    
    public TransactionLinkingService(
        AILinkingAgentBuilder agentBuilder,
        CryptoTrackerDbContext dbContext,
        ILogger<TransactionLinkingService> logger)
    {
        _agentBuilder = agentBuilder;
        _dbContext = dbContext;
        _logger = logger;
    }
    
    /// <summary>
    /// Startet eine neue Linking-Session
    /// </summary>
    public async Task<LinkingSessionResult> StartSessionAsync(CancellationToken ct = default)
    {
        var agent = _agentBuilder.BuildAgent(TransactionLinkingAgentDefinition.KEY);
        
        // Erste Nachricht: Agent analysiert die Situation
        var result = await agent.RunAsync(
            "Starte eine neue Verknüpfungs-Session. " +
            "Lade zuerst deine gespeicherten Regeln und dann die unverknüpften Transaktionen. " +
            "Gib mir einen Überblick über die Situation.",
            cancellationToken: ct);
        
        return new LinkingSessionResult
        {
            AgentResponse = result.Text ?? "",
            // Session-State könnte hier gespeichert werden
        };
    }
    
    /// <summary>
    /// Setzt eine Konversation mit dem Agent fort
    /// </summary>
    public async Task<LinkingSessionResult> ContinueSessionAsync(
        string userMessage, 
        CancellationToken ct = default)
    {
        var agent = _agentBuilder.BuildAgent(TransactionLinkingAgentDefinition.KEY);
        
        var result = await agent.RunAsync(userMessage, cancellationToken: ct);
        
        return new LinkingSessionResult
        {
            AgentResponse = result.Text ?? ""
        };
    }
    
    /// <summary>
    /// Führt automatische Verknüpfung durch (ohne Benutzerinteraktion)
    /// </summary>
    public async Task<AutoLinkResult> RunAutomaticLinkingAsync(CancellationToken ct = default)
    {
        var agent = _agentBuilder.BuildAgent(TransactionLinkingAgentDefinition.KEY);
        
        var result = await agent.RunAsync(
            "Führe automatische Verknüpfung durch: " +
            "1. Lade gespeicherte Regeln " +
            "2. Verknüpfe alle Transaktionen wo Zeit und Betrag exakt passen " +
            "3. Markiere bekannte externe Einnahmen (Staking, etc.) als intentionally unlinked " +
            "4. Gib mir eine Zusammenfassung was verknüpft wurde",
            cancellationToken: ct);
        
        // Zähle Ergebnisse
        var linkedCount = await _dbContext.TransactionLinkMetadata
            .CountAsync(m => m.LinkedAt > DateTimeOffset.UtcNow.AddMinutes(-5), ct);
        
        return new AutoLinkResult
        {
            LinkedCount = linkedCount,
            Summary = result.Text ?? ""
        };
    }
}

public record LinkingSessionResult
{
    public string AgentResponse { get; init; } = "";
    public List<TransactionLinkProposal>? Proposals { get; init; }
}

public record TransactionLinkProposal
{
    public int SendId { get; init; }
    public int ReceiveId { get; init; }
    public decimal Confidence { get; init; }
    public string Reason { get; init; } = "";
}

public record AutoLinkResult
{
    public int LinkedCount { get; init; }
    public string Summary { get; init; } = "";
}
```

---

## 6. API Controller

### 6.1 `TransactionLinkingController`

```csharp
namespace CryptoTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionLinkingController : ControllerBase
{
    private readonly TransactionLinkingService _linkingService;
    private readonly CryptoTrackerDbContext _dbContext;
    
    public TransactionLinkingController(
        TransactionLinkingService linkingService,
        CryptoTrackerDbContext dbContext)
    {
        _linkingService = linkingService;
        _dbContext = dbContext;
    }
    
    /// <summary>
    /// Startet eine neue Agent-Session
    /// </summary>
    [HttpPost("session/start")]
    public async Task<ActionResult<LinkingSessionResult>> StartSession(CancellationToken ct)
    {
        var result = await _linkingService.StartSessionAsync(ct);
        return Ok(result);
    }
    
    /// <summary>
    /// Sendet eine Nachricht an den Agent
    /// </summary>
    [HttpPost("session/message")]
    public async Task<ActionResult<LinkingSessionResult>> SendMessage(
        [FromBody] AgentMessageRequest request,
        CancellationToken ct)
    {
        var result = await _linkingService.ContinueSessionAsync(request.Message, ct);
        return Ok(result);
    }
    
    /// <summary>
    /// Führt automatische Verknüpfung durch
    /// </summary>
    [HttpPost("auto-link")]
    public async Task<ActionResult<AutoLinkResult>> RunAutoLink(CancellationToken ct)
    {
        var result = await _linkingService.RunAutomaticLinkingAsync(ct);
        return Ok(result);
    }
    
    /// <summary>
    /// Gibt alle unverknüpften Transaktionen zurück
    /// </summary>
    [HttpGet("unlinked")]
    public async Task<ActionResult<List<UnlinkedTransactionDto>>> GetUnlinked(
        [FromQuery] string? type = null,
        [FromQuery] string? symbol = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Where(t => t.OppositeTransactionId == null && !t.IsIntentionallyUnlinked);
        
        if (type == "send")
            query = query.Where(t => t.TransactionType == TransactionType.Send);
        else if (type == "receive")
            query = query.Where(t => t.TransactionType == TransactionType.Receive);
        
        if (!string.IsNullOrEmpty(symbol))
            query = query.Where(t => t.Symbol == symbol);
        
        var transactions = await query
            .OrderByDescending(t => t.DateTime)
            .Select(t => new UnlinkedTransactionDto
            {
                Id = t.Id,
                DateTime = t.DateTime,
                Type = t.TransactionType.ToString(),
                Symbol = t.Symbol,
                Quantity = t.Quantity,
                QuantityAfterFee = t.QuantityAfterFee,
                Comment = t.Comment,
                Address = t.Address,
                WalletName = t.Wallet.Name
            })
            .ToListAsync(ct);
        
        return Ok(transactions);
    }
    
    /// <summary>
    /// Manuell zwei Transaktionen verknüpfen
    /// </summary>
    [HttpPost("link")]
    public async Task<ActionResult> ManualLink([FromBody] ManualLinkRequest request, CancellationToken ct)
    {
        var send = await _dbContext.CryptoTransactions.FindAsync(request.SendId);
        var receive = await _dbContext.CryptoTransactions.FindAsync(request.ReceiveId);
        
        if (send == null || receive == null)
            return NotFound("Transaktion nicht gefunden");
        
        send.OppositeTransactionId = receive.Id;
        send.OppositeWalletId = receive.WalletId;
        receive.OppositeTransactionId = send.Id;
        receive.OppositeWalletId = send.WalletId;
        
        var metadata = new TransactionLinkMetadata
        {
            TransactionId = send.Id,
            LinkType = TransactionLinkType.Manual,
            Confidence = 1.0m,
            Reason = request.Reason ?? "Manuell verknüpft",
            LinkedAt = DateTimeOffset.UtcNow,
            IsConfirmed = true,
            ConfirmedAt = DateTimeOffset.UtcNow
        };
        _dbContext.TransactionLinkMetadata.Add(metadata);
        
        await _dbContext.SaveChangesAsync(ct);
        return Ok();
    }
    
    /// <summary>
    /// Bestätigt vorgeschlagene Verknüpfungen
    /// </summary>
    [HttpPost("confirm")]
    public async Task<ActionResult> ConfirmLinks([FromBody] ConfirmLinksRequest request, CancellationToken ct)
    {
        var metadataIds = request.TransactionIds;
        var metadataList = await _dbContext.TransactionLinkMetadata
            .Where(m => metadataIds.Contains(m.TransactionId))
            .ToListAsync(ct);
        
        foreach (var metadata in metadataList)
        {
            metadata.IsConfirmed = true;
            metadata.ConfirmedAt = DateTimeOffset.UtcNow;
        }
        
        await _dbContext.SaveChangesAsync(ct);
        return Ok(new { confirmed = metadataList.Count });
    }
}

public record AgentMessageRequest(string Message);
public record ManualLinkRequest(int SendId, int ReceiveId, string? Reason);
public record ConfirmLinksRequest(List<int> TransactionIds);
```

---

## 7. Blazor UI

### 7.1 Neue Seite: `TransaktionsPairing.razor`

```razor
@page "/transaktionen/pairing"
@using CryptoTracker.Client.Shared
@inject HttpClient Http
@inject IJSRuntime JS

<PageTitle>Transaktions-Verknüpfung</PageTitle>

<h1>Transaktions-Verknüpfungs-Assistent</h1>

<div class="row">
    <!-- Linke Spalte: Agent-Chat -->
    <div class="col-md-6">
        <div class="card">
            <div class="card-header d-flex justify-content-between">
                <span>KI-Assistent</span>
                <button class="btn btn-sm btn-outline-primary" @onclick="StartNewSession">
                    Neue Session
                </button>
            </div>
            <div class="card-body chat-container" style="height: 400px; overflow-y: auto;">
                @foreach (var message in _chatMessages)
                {
                    <div class="chat-message @(message.IsUser ? "user" : "assistant")">
                        <div class="message-content">@message.Content</div>
                        <div class="message-time">@message.Timestamp.ToString("HH:mm")</div>
                    </div>
                }
                @if (_isLoading)
                {
                    <div class="chat-message assistant">
                        <div class="message-content">
                            <span class="spinner-border spinner-border-sm"></span> Denke nach...
                        </div>
                    </div>
                }
            </div>
            <div class="card-footer">
                <div class="input-group">
                    <input type="text" class="form-control" @bind="_userInput" 
                           @onkeypress="HandleKeyPress" placeholder="Nachricht eingeben..." />
                    <button class="btn btn-primary" @onclick="SendMessage" disabled="@_isLoading">
                        Senden
                    </button>
                </div>
            </div>
        </div>
        
        <!-- Schnellaktionen -->
        <div class="card mt-3">
            <div class="card-header">Schnellaktionen</div>
            <div class="card-body">
                <button class="btn btn-success me-2" @onclick="RunAutoLink">
                    Automatisch verknüpfen
                </button>
                <button class="btn btn-warning" @onclick="ShowUnlinkedOverview">
                    Übersicht unverknüpfte
                </button>
            </div>
        </div>
    </div>
    
    <!-- Rechte Spalte: Vorschläge/Liste -->
    <div class="col-md-6">
        <div class="card">
            <div class="card-header">
                Unverknüpfte Transaktionen (@_unlinkedTransactions.Count)
            </div>
            <div class="card-body" style="max-height: 500px; overflow-y: auto;">
                @if (_proposals.Any())
                {
                    <h6>Vorgeschlagene Verknüpfungen</h6>
                    @foreach (var proposal in _proposals)
                    {
                        <div class="proposal-card mb-2 p-2 border rounded">
                            <div class="d-flex justify-content-between">
                                <span>Send #@proposal.SendId → Receive #@proposal.ReceiveId</span>
                                <span class="badge bg-info">@((proposal.Confidence * 100).ToString("0"))%</span>
                            </div>
                            <small class="text-muted">@proposal.Reason</small>
                            <div class="mt-1">
                                <button class="btn btn-sm btn-success" @onclick="() => ConfirmProposal(proposal)">
                                    Bestätigen
                                </button>
                                <button class="btn btn-sm btn-danger" @onclick="() => RejectProposal(proposal)">
                                    Ablehnen
                                </button>
                            </div>
                        </div>
                    }
                }
                
                <h6 class="mt-3">Unverknüpfte Transaktionen</h6>
                <table class="table table-sm">
                    <thead>
                        <tr>
                            <th>Datum</th>
                            <th>Typ</th>
                            <th>Symbol</th>
                            <th>Menge</th>
                            <th>Wallet</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var tx in _unlinkedTransactions.Take(20))
                        {
                            <tr @onclick="() => SelectTransaction(tx)" 
                                class="@(_selectedTransaction?.Id == tx.Id ? "table-active" : "")">
                                <td>@tx.DateTime.ToString("dd.MM.yy HH:mm")</td>
                                <td>@tx.Type</td>
                                <td>@tx.Symbol</td>
                                <td>@tx.QuantityAfterFee.ToString("0.####")</td>
                                <td>@tx.WalletName</td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        </div>
    </div>
</div>

@code {
    private List<ChatMessage> _chatMessages = new();
    private List<UnlinkedTransactionDto> _unlinkedTransactions = new();
    private List<TransactionLinkProposal> _proposals = new();
    private UnlinkedTransactionDto? _selectedTransaction;
    private string _userInput = "";
    private bool _isLoading;
    
    protected override async Task OnInitializedAsync()
    {
        await LoadUnlinkedTransactions();
    }
    
    private async Task StartNewSession()
    {
        _chatMessages.Clear();
        _isLoading = true;
        
        var result = await Http.PostAsJsonAsync("api/transactionlinking/session/start", new { });
        var response = await result.Content.ReadFromJsonAsync<LinkingSessionResult>();
        
        _chatMessages.Add(new ChatMessage
        {
            Content = response?.AgentResponse ?? "Session gestartet",
            IsUser = false,
            Timestamp = DateTime.Now
        });
        
        _isLoading = false;
    }
    
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(_userInput)) return;
        
        _chatMessages.Add(new ChatMessage
        {
            Content = _userInput,
            IsUser = true,
            Timestamp = DateTime.Now
        });
        
        var message = _userInput;
        _userInput = "";
        _isLoading = true;
        
        var result = await Http.PostAsJsonAsync("api/transactionlinking/session/message", 
            new AgentMessageRequest(message));
        var response = await result.Content.ReadFromJsonAsync<LinkingSessionResult>();
        
        _chatMessages.Add(new ChatMessage
        {
            Content = response?.AgentResponse ?? "Fehler",
            IsUser = false,
            Timestamp = DateTime.Now
        });
        
        if (response?.Proposals != null)
            _proposals = response.Proposals;
        
        _isLoading = false;
        await LoadUnlinkedTransactions();
    }
    
    private async Task RunAutoLink()
    {
        _isLoading = true;
        var result = await Http.PostAsJsonAsync("api/transactionlinking/auto-link", new { });
        var response = await result.Content.ReadFromJsonAsync<AutoLinkResult>();
        
        _chatMessages.Add(new ChatMessage
        {
            Content = $"Automatische Verknüpfung abgeschlossen: {response?.LinkedCount} Transaktionen verknüpft.\n\n{response?.Summary}",
            IsUser = false,
            Timestamp = DateTime.Now
        });
        
        _isLoading = false;
        await LoadUnlinkedTransactions();
    }
    
    private async Task LoadUnlinkedTransactions()
    {
        _unlinkedTransactions = await Http.GetFromJsonAsync<List<UnlinkedTransactionDto>>(
            "api/transactionlinking/unlinked") ?? new();
    }
    
    // ... weitere Methoden
    
    private record ChatMessage
    {
        public string Content { get; init; } = "";
        public bool IsUser { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
```

---

## 8. Lot-Verknüpfungs-Assistent

### 8.1 `LotLinkingAgentDefinition`

```csharp
namespace CryptoTracker.Agent.Definitions;

public sealed class LotLinkingAgentDefinition : AgentDefinitionBase
{
    public const string KEY = "lot-linking";
    
    private const string SystemPrompt = """
ROLLE
Du bist ein Experte für Krypto-Steuerrecht (Österreich) und Asset-Lot-Verwaltung.
Deine Aufgabe ist es, Lot-Ketten für die Steuerdokumentation zu vervollständigen.

KONTEXT - ÖSTERREICHISCHE KRYPTO-STEUER
- Neubestand (ab 01.03.2021): 27,5% KESt bei Verkauf
- Altbestand (vor 28.02.2021): Steuerfrei nach 1 Jahr Haltefrist
- Krypto-zu-Krypto-Tausch: Steuerfrei, aber Anschaffungskosten werden weitergegeben
- Vollständige Lot-Ketten sind für den Nachweis beim Finanzamt erforderlich

REGELN FÜR LOT-VERKNÜPFUNGEN
1. **Bei Transfers**: Das Lot wird auf die neue Wallet übertragen (ParentLotId setzen)
2. **Bei Swaps (Krypto→Krypto)**: 
   - Quell-Lots werden verbraucht (SourceLotId im Trade setzen)
   - Neues Lot erbt Anschaffungskosten und -datum
3. **Bei Verkäufen (Krypto→Fiat)**: Lot wird verbraucht, Gewinn/Verlust berechnet
4. **FIFO vs. Benutzerauswahl**: Bei mehreren Lots fragt den Benutzer

FLOW-VALIDIERUNG
Ein Lot hat einen vollständigen Flow wenn:
- Es direkt aus einem Fiat-Kauf stammt, ODER
- Es einen ParentLot hat, der seinerseits vollständig ist, ODER
- Es aus einem Swap mit vollständigen Quell-Lots stammt

TOOLS
- `get_unlinked_lots`: Lots ohne vollständigen Flow
- `get_lot_details`: Details zu einem Lot inkl. Kette
- `get_trades_without_source_lot`: Trades die Lot-Zuordnung brauchen
- `link_lot_to_trade`: Verknüpft ein Lot mit einem Trade
- `create_lot_chain`: Erstellt Lot-Kette für Transfer

OUTPUT
Antworte auf Deutsch. Erkläre steuerliche Auswirkungen bei Lot-Auswahl.
Bei Altbestand vs. Neubestand-Entscheidungen, weise auf die Konsequenzen hin.
""";

    public LotLinkingAgentDefinition(
        GetUnlinkedLotsTool getUnlinkedLotsTool,
        GetLotDetailsTool getLotDetailsTool,
        GetTradesWithoutSourceLotTool getTradesWithoutSourceLotTool,
        LinkLotToTradeTool linkLotToTradeTool,
        CreateLotChainTool createLotChainTool)
        : base(
            new AgentMetadata(KEY, "Lot-Verknüpfungs-Assistent", 
                "Vervollständigt Lot-Ketten für Steuerdokumentation"),
            new AgentPromptDefinition(SystemPrompt),
            [getUnlinkedLotsTool, getLotDetailsTool, getTradesWithoutSourceLotTool, 
             linkLotToTradeTool, createLotChainTool])
    {
    }
}
```

### 8.2 Workflow: Lot-Verknüpfung bei Swap

```
Szenario: Benutzer hat 10 ETH auf Binance, tauscht 5 ETH gegen BTC

Vorhandene Lots auf Binance:
- Lot #1: 3 ETH (Altbestand, Kauf 15.01.2020, 150€/ETH)
- Lot #2: 7 ETH (Neubestand, Kauf 15.03.2024, 3.000€/ETH)

Trade: Sell 5 ETH → Buy 0.15 BTC (Swap)

Agent fragt:
┌────────────────────────────────────────────────────────────────┐
│ Für den Swap von 5 ETH → 0.15 BTC müssen Lots ausgewählt      │
│ werden. Welche Lots sollen verwendet werden?                   │
│                                                                │
│ Option A: FIFO (Lot #1 zuerst)                                │
│   - 3 ETH aus Lot #1 (Altbestand, AK: 450€)                   │
│   - 2 ETH aus Lot #2 (Neubestand, AK: 6.000€)                 │
│   → Gesamt-AK für neues BTC-Lot: 6.450€                       │
│                                                                │
│ Option B: Nur Neubestand (Lot #2)                             │
│   - 5 ETH aus Lot #2 (Neubestand, AK: 15.000€)                │
│   → Gesamt-AK für neues BTC-Lot: 15.000€                      │
│                                                                │
│ Option C: Manuell auswählen                                   │
│                                                                │
│ Hinweis: Altbestand-ETH behalten = Bei späterem Fiat-Verkauf  │
│ steuerfrei!                                                    │
└────────────────────────────────────────────────────────────────┘

Nach Benutzerentscheidung:
- Agent ruft `link_lot_to_trade` für jeden verwendeten Lot auf
- Neues BTC-Lot wird erstellt mit aggregierten Anschaffungskosten
- Flow-Validierung wird aktualisiert
```

---

## 9. Konfiguration

### 9.1 `appsettings.json`

```json
{
  "OpenAI": {
    "Endpoint": "https://eastus2.api.cognitive.microsoft.com/",
    "DeploymentName": "gpt-5.2",
    "FastDeploymentName": "gpt-5-nano",
    "EmbeddingDeploymentName": "text-embedding-3-large",
    "EmbeddingVectorDimensions": 1536,
    "ApiKey": "xxx"
  }
}
```

### 9.2 OpenAI Settings Klasse

```csharp
namespace CryptoTracker.Agent.Common;

public class OpenAISettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = "gpt-5.2";
    public string FastDeploymentName { get; set; } = "gpt-5-nano";
    public string EmbeddingDeploymentName { get; set; } = "text-embedding-3-large";
    public int EmbeddingVectorDimensions { get; set; } = 1536;
    public string? ApiKey { get; set; }
}
```

### 9.3 Modell-Verwendung

| Modell | Verwendung | Anwendungsfall |
|--------|------------|----------------|
| **gpt-5.2** | Hauptmodell | Agent-Konversationen, komplexe Entscheidungen, Steuerberatung |
| **gpt-5-nano** | Fast-Modell | Schnelle Vergleiche, Muster-Matching, einfache Klassifizierung |
| **text-embedding-3-large** | Embeddings | Ähnlichkeitssuche für Kommentare, Semantic Search |

**Wann welches Modell:**
- `gpt-5.2`: Interaktive Agent-Konversationen, Lot-Auswahl mit Steuerberatung
- `gpt-5-nano`: Batch-Verarbeitung, schnelle Entscheidungen (z.B. "Ist das ein Staking Reward?")
- `text-embedding-3-large`: Suche nach ähnlichen Transaktionen/Kommentaren

### 9.4 `Startup.cs` / DI-Registrierung

```csharp
// OpenAI Settings
services.Configure<OpenAISettings>(configuration.GetSection("OpenAI"));

// Azure OpenAI Client (mit API Key oder DefaultAzureCredential)
services.AddSingleton(sp =>
{
    var settings = sp.GetRequiredService<IOptions<OpenAISettings>>().Value;
    if (!string.IsNullOrEmpty(settings.ApiKey))
    {
        return new AzureOpenAIClient(
            new Uri(settings.Endpoint),
            new AzureKeyCredential(settings.ApiKey));
    }
    return new AzureOpenAIClient(
        new Uri(settings.Endpoint),
        new DefaultAzureCredential());
});

// Agent Builder
services.AddScoped<AILinkingAgentBuilder>();

// Agent Definitions (als Services registrieren)
services.AddScoped<IAgentDefinition, TransactionLinkingAgentDefinition>();
services.AddScoped<IAgentDefinition, LotLinkingAgentDefinition>();

// Tools (Dependency Injection)
services.AddScoped<GetUnlinkedTransactionsTool>();
services.AddScoped<GetTransactionDetailsTool>();
services.AddScoped<LinkTransactionsTool>();
services.AddScoped<MarkAsIntentionallyUnlinkedTool>();
services.AddScoped<SaveAgentMemoryTool>();
services.AddScoped<GetAgentMemoryTool>();
services.AddScoped<GetUnlinkedLotsTool>();
services.AddScoped<GetLotDetailsTool>();
services.AddScoped<GetTradesWithoutSourceLotTool>();
services.AddScoped<LinkLotToTradeTool>();
services.AddScoped<CreateLotChainTool>();

// Services
services.AddScoped<TransactionLinkingService>();
services.AddScoped<LotLinkingService>();
```

### 9.5 Azure Bicep Infrastruktur

Siehe `infra/openai/openai.bicep` für Azure Deployment:

```bicep
// Hauptmodell (gpt-5.2)
param deploymentName string = 'gpt-5.2'
param deploymentModelName string = 'gpt-5.2'
param deploymentModelVersion string = '2025-11-13'
param deploymentCapacity int = 50

// Fast-Modell (gpt-5-nano) 
param fastDeploymentName string = 'gpt-5-nano'
param fastModelName string = 'gpt-5-nano'
param fastDeploymentModelVersion string = '2025-08-07'
param fastDeploymentCapacity int = 200

// Embeddings
param embeddingDeploymentName string = 'text-embedding-3-large'
param embeddingModelName string = 'text-embedding-3-large'
param embeddingModelVersion string = '1'
param embeddingDeploymentCapacity int = 350
```

---

## 10. Implementierungs-Arbeitspakete

### Phase 1: Grundlagen (Aufwand: ~16h)

| AP | Beschreibung | Aufwand |
|----|--------------|---------|
| 1.1 | NuGet-Pakete hinzufügen, OpenAI-Konfiguration | 2h |
| 1.2 | Neue Entities: `TransactionLinkMetadata`, `AgentMemory` | 3h |
| 1.3 | Migration erstellen und anwenden | 1h |
| 1.4 | Agent-Interfaces und Base-Klassen | 4h |
| 1.5 | `AILinkingAgentBuilder` implementieren | 4h |
| 1.6 | DI-Registrierung | 2h |

### Phase 2: Transaktions-Linking (Aufwand: ~20h)

| AP | Beschreibung | Aufwand |
|----|--------------|---------|
| 2.1 | Tools: `GetUnlinkedTransactionsTool`, `GetTransactionDetailsTool` | 4h |
| 2.2 | Tools: `LinkTransactionsTool`, `MarkAsIntentionallyUnlinkedTool` | 4h |
| 2.3 | Tools: `SaveAgentMemoryTool`, `GetAgentMemoryTool` | 3h |
| 2.4 | `TransactionLinkingAgentDefinition` mit System-Prompt | 3h |
| 2.5 | `TransactionLinkingService` | 3h |
| 2.6 | `TransactionLinkingController` | 3h |

### Phase 3: Lot-Linking (Aufwand: ~16h)

| AP | Beschreibung | Aufwand |
|----|--------------|---------|
| 3.1 | Tools: `GetUnlinkedLotsTool`, `GetLotDetailsTool` | 4h |
| 3.2 | Tools: `GetTradesWithoutSourceLotTool`, `LinkLotToTradeTool` | 4h |
| 3.3 | Tool: `CreateLotChainTool` | 3h |
| 3.4 | `LotLinkingAgentDefinition` mit System-Prompt | 3h |
| 3.5 | `LotLinkingService` und Controller | 2h |

### Phase 4: Blazor UI (Aufwand: ~20h)

| AP | Beschreibung | Aufwand |
|----|--------------|---------|
| 4.1 | `TransaktionsPairing.razor` - Chat-Interface | 6h |
| 4.2 | `TransaktionsPairing.razor` - Vorschlags-Liste | 4h |
| 4.3 | `LotVerknuepfung.razor` - Wizard | 6h |
| 4.4 | Gemeinsame Komponenten (Lot-Auswahl-Dialog) | 4h |

### Phase 5: Integration & Testing (Aufwand: ~12h)

| AP | Beschreibung | Aufwand |
|----|--------------|---------|
| 5.1 | Integration nach Import (automatisch Linking starten) | 3h |
| 5.2 | Unit-Tests für Tools | 4h |
| 5.3 | Integration-Tests für Agent-Workflows | 3h |
| 5.4 | Dokumentation | 2h |

**Gesamt: ~84h**

---

## 11. Link-Reset-Funktionalität

Da die alte `ProcessTransactionPairs()`-Methode bereits Verknüpfungen erstellt hat, die möglicherweise nicht korrekt sind, wird eine **Reset-Funktionalität** benötigt:

### 11.1 Anforderungen

- Alle bestehenden Transaktionsverknüpfungen zurücksetzen können
- Auch `TransactionLinkMetadata`-Einträge löschen
- Optional: Nur bestimmte Verknüpfungstypen zurücksetzen (z.B. nur `Automatic`, aber `Manual` behalten)
- `IsIntentionallyUnlinked`-Flags zurücksetzen (optional)

### 11.2 Neues Tool: `ResetAllLinksTool`

```csharp
public sealed class ResetAllLinksTool : IAgentTool
{
    public string GetToolName() => "reset_all_links";
    public string GetToolDescription() => """
        Setzt alle Transaktionsverknüpfungen zurück.
        Parameter:
        - keepManualLinks: true = Manuelle Verknüpfungen behalten
        - resetIntentionallyUnlinked: true = Auch bewusst unverknüpfte zurücksetzen
        ACHTUNG: Diese Aktion kann nicht rückgängig gemacht werden!
        """;
    
    private async Task<string> ResetAllLinksAsync(
        bool keepManualLinks = true,
        bool resetIntentionallyUnlinked = false)
    {
        // 1. Alle OppositeTransactionId/OppositeWalletId auf null setzen
        // 2. TransactionLinkMetadata löschen (außer Manual wenn keepManualLinks)
        // 3. Optional: IsIntentionallyUnlinked zurücksetzen
    }
}
```

### 11.3 API-Endpoint

```csharp
[HttpPost("reset")]
public async Task<ActionResult<ResetResult>> ResetLinks(
    [FromBody] ResetLinksRequest request,
    CancellationToken ct)
{
    // Sicherheitsabfrage: Nur mit expliziter Bestätigung
    if (!request.Confirmed)
        return BadRequest("Bestätigung erforderlich");
    
    var resetCount = await _linkingService.ResetAllLinksAsync(
        request.KeepManualLinks,
        request.ResetIntentionallyUnlinked,
        ct);
    
    return Ok(new ResetResult { ResetCount = resetCount });
}
```

### 11.4 UI: Reset-Dialog

Im `TransaktionsPairing.razor`:
- Button "Alle Verknüpfungen zurücksetzen"
- Bestätigungsdialog mit Warnung
- Checkboxen für Optionen (manuelle behalten, etc.)

---

## 12. Offene Punkte

1. **Session-Management**: Wie wird der Konversationsverlauf gespeichert?
   - Option A: In-Memory (geht bei Server-Restart verloren)
   - Option B: In DB speichern (AgentSession-Entity)
   - Option C: Redis/Cache

2. **Streaming**: Soll Agent-Antwort gestreamt werden?
   - Bessere UX bei langen Antworten
   - Benötigt SignalR oder Server-Sent Events

3. **Rate-Limiting**: Azure OpenAI hat TPM/RPM-Limits
   - Retry-Logic implementieren
   - Queue für Batch-Verarbeitung

4. **Kosten-Optimierung**: 
   - **gpt-5.2** für komplexe Agent-Konversationen und Steuerberatung
   - **gpt-5-nano** für schnelle Batch-Klassifizierung (z.B. "Ist das ein Staking Reward?")
   - **Embeddings** für Ähnlichkeitssuche bei Kommentaren
   - Caching von häufigen Mustern im AgentMemory

5. **Fehlerbehandlung**: Was wenn Agent halluziniert?
   - Validierung der Tool-Aufrufe
   - Rollback bei fehlerhaften Verknüpfungen

---

## 13. Nächste Schritte

1. ⬜ NuGet-Pakete hinzufügen und Build verifizieren
2. ⬜ Entities und Migration erstellen
3. ⬜ Erstes Tool implementieren und testen
4. ⬜ Agent-Definition mit System-Prompt
5. ⬜ Einfache Chat-UI zum Testen
6. ⬜ Reset-Funktionalität implementieren
7. ⬜ Iterativ Tools und Prompts verbessern

---

*Erstellt: 31.01.2026*
*Version: 1.1 - Reset-Funktionalität hinzugefügt*
