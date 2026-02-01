using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace CryptoTracker.Client.Shared;

public partial class LotLinkingWizard : IAsyncDisposable
{
    [Parameter] public EventCallback OnComplete { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }
    [Parameter] public LotLinkingStatisticsDTO? Statistics { get; set; }
    
    [Inject] private ILotsApi LotsApi { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

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
    private IList<LotOptionDTO>? CurrentLotOptions;
    private List<LotAllocationInput> AllocationInputs = new();
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
    private const string FreeTextOptionLabel = "Andere Option (Freitext)";
    private bool BodyLockApplied = false;

    private int ProgressPercent => TotalCount > 0 ? (int)(ProcessedCount * 100.0 / TotalCount) : 0;
    private bool HasFreeTextOption => CurrentOptions?.Any(o => o.Equals(FreeTextOptionLabel, StringComparison.OrdinalIgnoreCase)) == true;
    private decimal RequiredQuantity => CurrentAssignment?.Quantity ?? 0m;
    private const decimal AllocationTolerance = 0.00000001m;
    private bool HasAllocations => RequiredQuantity > 0
        && Math.Abs(TotalAllocatedQuantity - RequiredQuantity) <= AllocationTolerance;

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
                    CurrentLotOptions = evt.LotOptions;
                    InitializeLotAllocations(evt.LotOptions);
                    ShowFreeTextInput = false;
                    FreeTextInput = null;
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

                case "info":
                    AddEvent("info", evt.Message);
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
                CurrentLotOptions = session.CurrentLotOptions;
                InitializeLotAllocations(session.CurrentLotOptions);
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SetBodyLockAsync(true);
        }
    }

    private async Task OnOptionSelected(string option)
    {
        if (string.Equals(option, FreeTextOptionLabel, StringComparison.OrdinalIgnoreCase))
        {
            ShowFreeText();
            return;
        }

        await AnswerQuestion(option);
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
                CustomText = string.IsNullOrWhiteSpace(FreeTextInput) ? null : FreeTextInput
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
            CurrentLotOptions = null;
            AllocationInputs = new List<LotAllocationInput>();
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

    private void InitializeLotAllocations(IList<LotOptionDTO>? options)
    {
        AllocationInputs = options?
            .Select(o => new LotAllocationInput
            {
                LotId = o.LotId,
                DisplayText = o.DisplayText,
                AvailableQuantity = o.AvailableQuantity
            })
            .ToList() ?? new List<LotAllocationInput>();
    }

    private void UpdateQuantity(LotAllocationInput input)
    {
        if (input.Quantity < 0)
            input.Quantity = 0;
        if (input.Quantity > input.AvailableQuantity)
            input.Quantity = input.AvailableQuantity;

        input.Percent = RequiredQuantity > 0
            ? Math.Round(input.Quantity / RequiredQuantity * 100m, 4)
            : 0m;
    }

    private void OnQuantityInput(ChangeEventArgs e, LotAllocationInput input)
    {
        if (e.Value == null)
        {
            input.Quantity = 0m;
            UpdateQuantity(input);
            return;
        }

        if (decimal.TryParse(e.Value.ToString(), out var value))
        {
            input.Quantity = value;
            UpdateQuantity(input);
        }
    }

    private void UpdatePercent(LotAllocationInput input)
    {
        if (input.Percent < 0)
            input.Percent = 0;
        if (input.Percent > 100)
            input.Percent = 100;

        input.Quantity = RequiredQuantity > 0
            ? Math.Round(RequiredQuantity * input.Percent / 100m, 8)
            : 0m;

        if (input.Quantity > input.AvailableQuantity)
        {
            input.Quantity = input.AvailableQuantity;
            input.Percent = RequiredQuantity > 0
                ? Math.Round(input.Quantity / RequiredQuantity * 100m, 4)
                : 0m;
        }
    }

    private void OnPercentInput(ChangeEventArgs e, LotAllocationInput input)
    {
        if (e.Value == null)
        {
            input.Percent = 0m;
            UpdatePercent(input);
            return;
        }

        if (decimal.TryParse(e.Value.ToString(), out var value))
        {
            input.Percent = value;
            UpdatePercent(input);
        }
    }

    private decimal TotalAllocatedQuantity => AllocationInputs.Sum(a => a.Quantity);
    private decimal TotalAllocatedPercent => RequiredQuantity > 0
        ? Math.Round(TotalAllocatedQuantity / RequiredQuantity * 100m, 2)
        : 0m;

    private async Task SubmitAllocations()
    {
        if (CurrentQuestionId == null || SessionId == null)
            return;

        var allocations = AllocationInputs
            .Where(a => a.Quantity > 0)
            .Select(a => new LotAllocationDTO(a.LotId, a.Quantity))
            .ToList();

        if (allocations.Count == 0 || !HasAllocations)
            return;

        IsAnswering = true;
        StateHasChanged();

        try
        {
            var response = new LotLinkingUserResponseDTO
            {
                QuestionId = CurrentQuestionId,
                Response = "LOT_ALLOCATIONS",
                ShouldRemember = ShouldRemember,
                LotAllocations = allocations,
                CustomText = string.IsNullOrWhiteSpace(FreeTextInput) ? null : FreeTextInput
            };

            if (hubConnection?.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SendLotLinkingResponse", SessionId, response);
            }

            CurrentQuestionId = null;
            CurrentQuestion = null;
            CurrentAssignment = null;
            CurrentOptions = null;
            CurrentLotOptions = null;
            AllocationInputs = new List<LotAllocationInput>();
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

    private async Task SubmitFreeText()
    {
        if (string.IsNullOrWhiteSpace(FreeTextInput)) return;

        await AnswerQuestion(FreeTextOptionLabel);
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
        if (BodyLockApplied)
        {
            await SetBodyLockAsync(false);
        }

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

    private async Task SetBodyLockAsync(bool isLocked)
    {
        if (JsRuntime == null)
        {
            return;
        }

        BodyLockApplied = isLocked;
        await JsRuntime.InvokeVoidAsync("cryptoTracker.setWizardOpen", isLocked);
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
            "info" => "i",
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
            "info" => "info",
            _ => ""
        };
    }

    private sealed class LotAllocationInput
    {
        public int LotId { get; set; }
        public string DisplayText { get; set; } = "";
        public decimal AvailableQuantity { get; set; }
        public decimal Quantity { get; set; }
        public decimal Percent { get; set; }
    }
}
