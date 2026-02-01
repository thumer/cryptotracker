using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace CryptoTracker.Client.Shared;

public partial class LinkingWizard : IAsyncDisposable
{
    [Parameter] public EventCallback OnComplete { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }
    [Parameter] public LinkingStatisticsDTO? Statistics { get; set; }
    
    [Inject] private ITransactionLinkingApi LinkingApi { get; set; } = default!;
    [Inject] private IWalletApi WalletApi { get; set; } = default!;
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
    private int LinkedCount = 0;
    private int MarkedExternalCount = 0;
    private int SkippedCount = 0;
    private string ProcessingMessage = "Starte...";

    // Question state
    private string? CurrentQuestionId;
    private string? CurrentQuestion;
    private UnlinkedTransactionDTO? CurrentTransaction;
    private IList<string>? CurrentOptions;
    private bool ShouldRemember = true;
    private bool ShowFreeTextInput = false;
    private string? FreeTextInput;
    private bool ShowVirtualWalletSelection = false;
    private bool IsLoadingVirtualWallets = false;
    private IList<WalletInfoDTO> VirtualWallets = new List<WalletInfoDTO>();
    private int? SelectedVirtualWalletId;
    private string? NewVirtualWalletName;

    // Learned rules
    private List<LearnedRuleDTO> LearnedRules = new();
    private bool RulesExpanded = false;

    // Event log
    private List<EventLogEntry> EventLog = new();
    private ElementReference eventLogElement;

    // Session
    private string? SessionId;
    private HubConnection? hubConnection;
    private const string VirtualWalletOptionLabel = "Gegenstück in virtuelles Wallet buchen";

    private int ProgressPercent => TotalCount > 0 ? (int)(ProcessedCount * 100.0 / TotalCount) : 0;

    protected override async Task OnInitializedAsync()
    {
        // Load learned rules
        try
        {
            var rules = await LinkingApi.GetLearnedRulesAsync();
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
            var session = await LinkingApi.StartInteractiveSessionAsync();
            SessionId = session.SessionId;
            TotalCount = session.TotalCount;

            // Build SignalR connection
            var hubUrl = NavigationManager.ToAbsoluteUri("/hubs/linking");
            hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            // Register event handlers
            hubConnection.On<LinkingEventDTO>("OnLinkingEvent", HandleLinkingEvent);
            hubConnection.On<InteractiveLinkingSessionDTO>("OnSessionUpdate", HandleSessionUpdate);
            hubConnection.On<string>("OnError", HandleError);
            hubConnection.On<LinkingStatisticsDTO>("OnSessionCompleted", HandleSessionCompleted);

            // Connect and join session
            await hubConnection.StartAsync();
            await hubConnection.InvokeAsync("JoinSession", SessionId);

            IsStarted = true;
            IsProcessing = true;
            ProcessingMessage = "Analysiere Transaktionen...";
            AddEvent("start", "Interaktive Verknüpfung gestartet");
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
            await LinkingApi.StopInteractiveSessionAsync(SessionId);
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

    private void HandleLinkingEvent(LinkingEventDTO evt)
    {
        InvokeAsync(() =>
        {
            switch (evt.EventType)
            {
                case "linked":
                    LinkedCount++;
                    ProcessedCount = evt.ProcessedCount;
                    AddEvent("linked", evt.Message);
                    break;

                case "marked_external":
                    MarkedExternalCount++;
                    ProcessedCount = evt.ProcessedCount;
                    AddEvent("external", evt.Message);
                    break;

                case "skipped":
                    SkippedCount++;
                    ProcessedCount = evt.ProcessedCount;
                    AddEvent("skipped", evt.Message);
                    break;

                case "question":
                    CurrentQuestionId = evt.QuestionId;
                    CurrentQuestion = evt.Message;
                    CurrentTransaction = evt.Transaction;
                    CurrentOptions = evt.Options;
                    ShowVirtualWalletSelection = false;
                    SelectedVirtualWalletId = null;
                    NewVirtualWalletName = null;
                    IsProcessing = false;
                    break;

                case "progress":
                    ProcessedCount = evt.ProcessedCount;
                    TotalCount = evt.TotalCount;
                    ProcessingMessage = evt.Message;
                    break;

                case "rule_learned":
                    AddEvent("rule", evt.Message);
                    // Refresh rules list
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

    private void HandleSessionUpdate(InteractiveLinkingSessionDTO session)
    {
        InvokeAsync(() =>
        {
            ProcessedCount = session.ProcessedCount;
            TotalCount = session.TotalCount;
            LinkedCount = session.LinkedCount;
            MarkedExternalCount = session.MarkedExternalCount;
            SkippedCount = session.SkippedCount;

            if (session.CurrentQuestionId != null)
            {
                CurrentQuestionId = session.CurrentQuestionId;
                CurrentQuestion = session.CurrentQuestion;
                CurrentTransaction = session.CurrentTransaction;
                CurrentOptions = session.CurrentOptions;
                ShowVirtualWalletSelection = false;
                SelectedVirtualWalletId = null;
                NewVirtualWalletName = null;
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

    private void HandleSessionCompleted(LinkingStatisticsDTO stats)
    {
        InvokeAsync(() =>
        {
            IsCompleted = true;
            IsProcessing = false;
            CurrentQuestion = null;
            ShowVirtualWalletSelection = false;
            AddEvent("complete", $"Fertig! {LinkedCount} verknüpft, {MarkedExternalCount} extern, {SkippedCount} übersprungen");
            StateHasChanged();
        });
    }

    private async Task OnOptionSelected(string option)
    {
        if (option == VirtualWalletOptionLabel)
        {
            ShowVirtualWalletSelection = true;
            ShowFreeTextInput = false;
            FreeTextInput = null;
            await LoadVirtualWalletsAsync();
            StateHasChanged();
            return;
        }

        await AnswerQuestion(option);
    }

    private async Task AnswerQuestion(string answer)
    {
        if (CurrentQuestionId == null) return;

        var response = new UserResponseDTO
        {
            QuestionId = CurrentQuestionId,
            Response = answer,
            ShouldRemember = ShouldRemember
        };

        await SendResponseAsync(response);
    }

    private async Task SendResponseAsync(UserResponseDTO response)
    {
        if (SessionId == null) return;

        IsAnswering = true;
        StateHasChanged();

        try
        {
            // Send via SignalR
            if (hubConnection?.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SendUserResponse", SessionId, response);
            }
            else
            {
                // Fallback to API
                await LinkingApi.SubmitUserResponseAsync(SessionId, response);
            }

            // Clear question
            CurrentQuestionId = null;
            CurrentQuestion = null;
            CurrentTransaction = null;
            CurrentOptions = null;
            ShowFreeTextInput = false;
            FreeTextInput = null;
            ShowVirtualWalletSelection = false;
            SelectedVirtualWalletId = null;
            NewVirtualWalletName = null;
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
            var rules = await LinkingApi.GetLearnedRulesAsync();
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
            var success = await LinkingApi.DeleteLearnedRuleAsync(ruleId);
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
        ShowVirtualWalletSelection = false;
        SelectedVirtualWalletId = null;
        NewVirtualWalletName = null;
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

    private async Task LoadVirtualWalletsAsync()
    {
        if (IsLoadingVirtualWallets)
            return;

        IsLoadingVirtualWallets = true;
        try
        {
            var wallets = await WalletApi.GetVirtualWalletInfosAsync();
            VirtualWallets = wallets
                .Where(w => w.IsVirtual)
                .OrderBy(w => w.Name)
                .ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Fehler beim Laden der virtuellen Wallets: {ex.Message}";
        }
        finally
        {
            IsLoadingVirtualWallets = false;
        }
    }

    private async Task LinkWithExistingVirtualWallet()
    {
        if (CurrentQuestionId == null || SelectedVirtualWalletId == null)
            return;

        var response = new UserResponseDTO
        {
            QuestionId = CurrentQuestionId,
            Response = VirtualWalletOptionLabel,
            ShouldRemember = ShouldRemember,
            Action = "virtual_wallet",
            VirtualWalletId = SelectedVirtualWalletId
        };

        await SendResponseAsync(response);
    }

    private async Task CreateAndLinkVirtualWallet()
    {
        if (CurrentQuestionId == null)
            return;

        var walletName = NewVirtualWalletName?.Trim();
        if (string.IsNullOrWhiteSpace(walletName))
            return;

        var response = new UserResponseDTO
        {
            QuestionId = CurrentQuestionId,
            Response = VirtualWalletOptionLabel,
            ShouldRemember = ShouldRemember,
            Action = "virtual_wallet",
            VirtualWalletName = walletName
        };

        await SendResponseAsync(response);
        await LoadVirtualWalletsAsync();
    }

    private void CancelVirtualWalletSelection()
    {
        ShowVirtualWalletSelection = false;
        SelectedVirtualWalletId = null;
        NewVirtualWalletName = null;
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
            var o when o.Contains("extern") || o.Contains("ja") => "btn-success",
            var o when o.Contains("virtuell") => "btn-outline-primary",
            var o when o.Contains("skip") || o.Contains("überspringen") => "btn-secondary",
            var o when o.Contains("nein") => "btn-outline-secondary",
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
            "linked" => "✓",
            "external" => "⊕",
            "skipped" => "⏭",
            "error" => "⚠",
            "rule" => "📝",
            "start" => "▶",
            "stop" => "⏹",
            "complete" => "🎉",
            _ => "•"
        };

        public string CssClass => Type switch
        {
            "linked" => "linked",
            "external" => "external",
            "skipped" => "skipped",
            "error" => "error",
            "rule" => "rule",
            _ => ""
        };
    }
}
