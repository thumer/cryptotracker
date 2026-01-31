using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace CryptoTracker.Client.Shared;

public partial class LotLinkingWizard : IAsyncDisposable
{
    [Parameter] public EventCallback OnComplete { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }
    [Parameter] public LotLinkingStatisticsDTO? Statistics { get; set; }
    
    [Inject] private ILotsApi LotsApi { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    // State
    private bool IsStarted = false;
    private bool IsConnecting = false;
    private bool IsProcessing = false;
    private bool IsCompleted = false;
    private bool IsAnswering = false;
    private string? ErrorMessage;

    // Progress
    private int ProcessedCount = 0;
    private int TotalCount = 0;
    private int AssignedCount = 0;
    private int CreatedLotsCount = 0;
    private int SkippedCount = 0;
    private string ProcessingMessage = "Starte...";

    // Question state
    private string? CurrentQuestionId;
    private string? CurrentQuestion;
    private PendingLotAssignmentDTO? CurrentAssignment;
    private IList<string>? CurrentOptions;
    private bool ShouldRemember = true;
    private bool RequiresPriceInput = false;
    private decimal? InputAcquisitionPrice;
    private bool ShowFreeTextInput = false;
    private string? FreeTextInput;

    // Learned rules
    private List<LotLinkingRuleDTO> LearnedRules = new();
    private bool RulesExpanded = false;

    // Event log
    private List<EventLogEntry> EventLog = new();
    private ElementReference eventLogElement;

    // Session
    private string? SessionId;
    private HubConnection? hubConnection;

    private int ProgressPercent => TotalCount > 0 ? (int)(ProcessedCount * 100.0 / TotalCount) : 0;

    protected override async Task OnInitializedAsync()
    {
        // Load learned rules
        try
        {
            var rules = await LotsApi.GetLotLinkingRulesAsync();
            LearnedRules = rules.ToList();
        }
        catch
        {
            // Ignore - rules are optional
        }
    }

    private string GetStatusText()
    {
        if (!IsStarted) return "Bereit zum Starten";
        if (IsCompleted) return "Abgeschlossen";
        if (CurrentQuestion != null) return "Warte auf Eingabe";
        return $"Verarbeite... ({ProcessedCount}/{TotalCount})";
    }

    private async Task StartInteractiveSession()
    {
        IsConnecting = true;
        ErrorMessage = null;
        StateHasChanged();

        try
        {
            // Start session via API
            var session = await LotsApi.StartInteractiveLotLinkingSessionAsync();
            SessionId = session.SessionId;
            TotalCount = session.TotalCount;

            // Build SignalR connection
            var hubUrl = NavigationManager.ToAbsoluteUri("/hubs/linking");
            hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            // Register event handlers
            hubConnection.On<LotLinkingEventDTO>("OnLotLinkingEvent", HandleLotLinkingEvent);
            hubConnection.On<InteractiveLotLinkingSessionDTO>("OnLotLinkingSessionUpdate", HandleSessionUpdate);
            hubConnection.On<string>("OnLotLinkingError", HandleError);
            hubConnection.On<LotLinkingStatisticsDTO>("OnLotLinkingSessionCompleted", HandleSessionCompleted);

            // Connect and join session
            await hubConnection.StartAsync();
            await hubConnection.InvokeAsync("JoinSession", SessionId);

            IsStarted = true;
            IsProcessing = true;
            ProcessingMessage = "Analysiere ausstehende Zuordnungen...";
            AddEvent("start", "Interaktive Lot-Zuordnung gestartet");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Fehler beim Starten: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
            StateHasChanged();
        }
    }

    private async Task StopSession()
    {
        if (SessionId == null) return;

        try
        {
            await LotsApi.StopInteractiveLotLinkingSessionAsync(SessionId);
            AddEvent("stop", "Session abgebrochen");
            IsCompleted = true;
            IsProcessing = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Fehler beim Stoppen: {ex.Message}";
        }

        StateHasChanged();
    }

    private void HandleLotLinkingEvent(LotLinkingEventDTO evt)
    {
        InvokeAsync(() =>
        {
            switch (evt.EventType)
            {
                case "assigned":
                    AssignedCount++;
                    ProcessedCount = evt.ProcessedCount;
                    AddEvent("assigned", evt.Message);
                    break;

                case "lot_created":
                    CreatedLotsCount++;
                    ProcessedCount = evt.ProcessedCount;
                    AddEvent("lot_created", evt.Message);
                    break;

                case "skipped":
                    SkippedCount++;
                    ProcessedCount = evt.ProcessedCount;
                    AddEvent("skipped", evt.Message);
                    break;

                case "question":
                    CurrentQuestionId = evt.QuestionId;
                    CurrentQuestion = evt.Message;
                    CurrentAssignment = evt.Assignment;
                    CurrentOptions = evt.Options;
                    RequiresPriceInput = evt.Message?.Contains("Woher stammen") == true;
                    InputAcquisitionPrice = null;
                    IsProcessing = false;
                    break;

                case "progress":
                    ProcessedCount = evt.ProcessedCount;
                    TotalCount = evt.TotalCount;
                    ProcessingMessage = evt.Message;
                    break;

                case "rule_learned":
                    AddEvent("rule", evt.Message);
                    _ = RefreshRulesAsync();
                    break;

                case "error":
                    AddEvent("error", evt.Message);
                    break;
            }

            StateHasChanged();
            _ = ScrollEventLogToBottom();
        });
    }

    private void HandleSessionUpdate(InteractiveLotLinkingSessionDTO session)
    {
        InvokeAsync(() =>
        {
            ProcessedCount = session.ProcessedCount;
            TotalCount = session.TotalCount;
            AssignedCount = session.AssignedCount;
            CreatedLotsCount = session.CreatedLotsCount;
            SkippedCount = session.SkippedCount;

            if (session.CurrentQuestionId != null)
            {
                CurrentQuestionId = session.CurrentQuestionId;
                CurrentQuestion = session.CurrentQuestion;
                CurrentAssignment = session.CurrentAssignment;
                CurrentOptions = session.CurrentOptions;
                IsProcessing = false;
            }

            StateHasChanged();
        });
    }

    private void HandleError(string error)
    {
        InvokeAsync(() =>
        {
            ErrorMessage = error;
            AddEvent("error", error);
            StateHasChanged();
        });
    }

    private void HandleSessionCompleted(LotLinkingStatisticsDTO stats)
    {
        InvokeAsync(() =>
        {
            IsCompleted = true;
            IsProcessing = false;
            CurrentQuestion = null;
            AddEvent("complete", $"Fertig! {CreatedLotsCount} Lots erstellt, {AssignedCount} zugeordnet, {SkippedCount} übersprungen");
            StateHasChanged();
        });
    }

    private async Task AnswerQuestion(string answer)
    {
        if (CurrentQuestionId == null || SessionId == null) return;

        IsAnswering = true;
        StateHasChanged();

        try
        {
            var response = new LotLinkingUserResponseDTO
            {
                QuestionId = CurrentQuestionId,
                Response = answer,
                ShouldRemember = ShouldRemember,
                AcquisitionPriceEur = InputAcquisitionPrice,
                CustomText = answer.StartsWith("[Freitext]") ? answer.Replace("[Freitext] ", "") : null
            };

            // Send via SignalR
            if (hubConnection?.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SendLotLinkingResponse", SessionId, response);
            }

            // Clear question
            CurrentQuestionId = null;
            CurrentQuestion = null;
            CurrentAssignment = null;
            CurrentOptions = null;
            RequiresPriceInput = false;
            InputAcquisitionPrice = null;
            ShowFreeTextInput = false;
            FreeTextInput = null;
            IsProcessing = true;
            ProcessingMessage = "Verarbeite Antwort...";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Fehler beim Senden: {ex.Message}";
        }
        finally
        {
            IsAnswering = false;
            StateHasChanged();
        }
    }

    private async Task RefreshRulesAsync()
    {
        try
        {
            var rules = await LotsApi.GetLotLinkingRulesAsync();
            LearnedRules = rules.ToList();
            StateHasChanged();
        }
        catch
        {
            // Ignore
        }
    }

    private async Task DeleteRule(string ruleId)
    {
        try
        {
            var success = await LotsApi.DeleteLotLinkingRuleAsync(ruleId);
            if (success)
            {
                LearnedRules.RemoveAll(r => r.Id == ruleId);
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Fehler beim Löschen: {ex.Message}";
            StateHasChanged();
        }
    }

    private void ToggleRulesExpanded()
    {
        RulesExpanded = !RulesExpanded;
    }

    private void ShowFreeText()
    {
        ShowFreeTextInput = true;
        FreeTextInput = null;
    }

    private void CancelFreeText()
    {
        ShowFreeTextInput = false;
        FreeTextInput = null;
    }

    private async Task SubmitFreeText()
    {
        if (string.IsNullOrWhiteSpace(FreeTextInput)) return;
        
        // Send free text as the answer with a prefix to identify it
        await AnswerQuestion($"[Freitext] {FreeTextInput}");
    }

    private void AddEvent(string type, string message)
    {
        EventLog.Add(new EventLogEntry
        {
            Type = type,
            Message = message,
            Timestamp = DateTime.Now
        });

        // Keep only last 100 events
        if (EventLog.Count > 100)
        {
            EventLog.RemoveAt(0);
        }
    }

    private async Task ScrollEventLogToBottom()
    {
        try
        {
            // This would require JS interop - for now we skip it
            await Task.CompletedTask;
        }
        catch
        {
            // Ignore
        }
    }

    private string GetOptionButtonClass(string option)
    {
        return option.ToLowerInvariant() switch
        {
            var o when o.Contains("airdrop") => "btn-info",
            var o when o.Contains("staking") => "btn-info",
            var o when o.Contains("mining") => "btn-info",
            var o when o.Contains("extern") || o.Contains("eingang") => "btn-success",
            var o when o.Contains("skip") || o.Contains("überspringen") => "btn-secondary",
            var o when o.Contains("schenkung") || o.Contains("erbe") => "btn-warning",
            _ => "btn-primary"
        };
    }

    private async Task Complete()
    {
        await OnComplete.InvokeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection != null)
        {
            if (SessionId != null)
            {
                try
                {
                    await hubConnection.InvokeAsync("LeaveSession", SessionId);
                }
                catch
                {
                    // Ignore
                }
            }
            await hubConnection.DisposeAsync();
        }
    }

    private class EventLogEntry
    {
        public string Type { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }

        public string Icon => Type switch
        {
            "assigned" => "->",
            "lot_created" => "+",
            "skipped" => ">>",
            "error" => "!",
            "rule" => "#",
            "start" => ">",
            "stop" => "[]",
            "complete" => "OK",
            _ => "*"
        };

        public string CssClass => Type switch
        {
            "assigned" => "assigned",
            "lot_created" => "lot_created",
            "skipped" => "skipped",
            "error" => "error",
            "rule" => "rule",
            _ => ""
        };
    }
}
