using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CryptoTracker.Client.Pages;

public partial class TransactionLinking
{
    // State
    private bool IsLoading = true;
    private bool IsAIConfigured = false;
    private LinkingStatisticsDTO? Statistics;
    private IList<UnlinkedTransactionDTO> UnlinkedTransactions = new List<UnlinkedTransactionDTO>();
    private IList<UnlinkedTransactionDTO> SelectedTransactions = new List<UnlinkedTransactionDTO>();
    private string? SelectedSymbol;
    private string? SelectedType;
    private string? SuccessMessage;

    // Wizard State
    private bool IsWizardOpen = false;

    // Chat State
    private bool IsChatOpen = false;
    private bool IsChatLoading = false;
    private string ChatInput = "";
    private List<ChatMessageDTO> ChatMessages = new();
    private ElementReference ChatMessagesRef;

    // Link Dialog State
    private bool IsLinkDialogOpen = false;
    private UnlinkedTransactionDTO? LinkDialogTransaction;
    private IList<UnlinkedTransactionDTO> PotentialMatches = new List<UnlinkedTransactionDTO>();
    private UnlinkedTransactionDTO? SelectedMatch;
    private string? LinkReason;
    private bool IsLoadingMatches = false;
    private bool IsLinking = false;
    private string? LinkError;

    // Mark External Dialog State
    private bool IsMarkExternalDialogOpen = false;
    private UnlinkedTransactionDTO? MarkExternalTransaction;
    private string ExternalReason = "";
    private bool IsMarkingExternal = false;
    private string? MarkExternalError;

    // Reset Dialog State
    private bool IsResetDialogOpen = false;
    private bool ResetKeepManual = true;
    private bool ResetIncludeExternal = false;
    private bool IsResetting = false;

    // Auto-Link State
    private bool IsAutoLinking = false;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    // Wizard
    private void OpenWizard()
    {
        IsWizardOpen = true;
    }

    private async Task CloseWizard()
    {
        IsWizardOpen = false;
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            // Sequential calls to avoid DbContext threading issues
            var status = await LinkingApi.GetStatusAsync();
            IsAIConfigured = status.IsConfigured;

            Statistics = await LinkingApi.GetStatisticsAsync();
            UnlinkedTransactions = await LinkingApi.GetUnlinkedAsync(SelectedType, SelectedSymbol);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading data: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task FilterBySymbol(string symbol)
    {
        SelectedSymbol = symbol;
        await LoadDataAsync();
    }

    private async Task ClearSymbolFilter()
    {
        SelectedSymbol = null;
        await LoadDataAsync();
    }

    private async Task FilterByType(string? type)
    {
        SelectedType = type;
        await LoadDataAsync();
    }

    // Selection
    private void SelectAll(bool selected)
    {
        if (selected)
            SelectedTransactions = UnlinkedTransactions.ToList();
        else
            SelectedTransactions = new List<UnlinkedTransactionDTO>();
    }

    private void ToggleSelection(UnlinkedTransactionDTO tx, bool selected)
    {
        var list = SelectedTransactions.ToList();
        if (selected && !list.Contains(tx))
            list.Add(tx);
        else if (!selected)
            list.Remove(tx);
        SelectedTransactions = list;
    }

    // Chat
    private void ToggleChat()
    {
        IsChatOpen = !IsChatOpen;
        if (IsChatOpen && ChatMessages.Count == 0)
        {
            _ = StartChatSession();
        }
    }

    private async Task StartChatSession()
    {
        IsChatLoading = true;
        ChatMessages.Add(new ChatMessageDTO { Role = "assistant", Content = "", IsLoading = true });
        StateHasChanged();

        try
        {
            var result = await LinkingApi.StartSessionAsync();
            ChatMessages.RemoveAt(ChatMessages.Count - 1);
            ChatMessages.Add(new ChatMessageDTO 
            { 
                Role = "assistant", 
                Content = result.AgentResponse,
                Timestamp = DateTimeOffset.Now
            });
        }
        catch (Exception ex)
        {
            ChatMessages.RemoveAt(ChatMessages.Count - 1);
            ChatMessages.Add(new ChatMessageDTO 
            { 
                Role = "assistant", 
                Content = $"Fehler: {ex.Message}",
                Timestamp = DateTimeOffset.Now
            });
        }
        finally
        {
            IsChatLoading = false;
            StateHasChanged();
        }
    }

    private async Task OnChatKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(ChatInput))
        {
            await SendChatMessage();
        }
    }

    private async Task SendChatMessage()
    {
        if (string.IsNullOrWhiteSpace(ChatInput) || IsChatLoading)
            return;

        var userMessage = ChatInput;
        ChatInput = "";

        ChatMessages.Add(new ChatMessageDTO 
        { 
            Role = "user", 
            Content = userMessage,
            Timestamp = DateTimeOffset.Now
        });

        IsChatLoading = true;
        ChatMessages.Add(new ChatMessageDTO { Role = "assistant", Content = "", IsLoading = true });
        StateHasChanged();

        try
        {
            var result = await LinkingApi.SendMessageAsync(userMessage);
            ChatMessages.RemoveAt(ChatMessages.Count - 1);
            ChatMessages.Add(new ChatMessageDTO 
            { 
                Role = "assistant", 
                Content = result.AgentResponse,
                Timestamp = DateTimeOffset.Now
            });

            // Refresh data after agent interaction
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ChatMessages.RemoveAt(ChatMessages.Count - 1);
            ChatMessages.Add(new ChatMessageDTO 
            { 
                Role = "assistant", 
                Content = $"Fehler: {ex.Message}",
                Timestamp = DateTimeOffset.Now
            });
        }
        finally
        {
            IsChatLoading = false;
            StateHasChanged();
        }
    }

    // Auto-Link
    private async Task RunAutoLink()
    {
        IsAutoLinking = true;
        StateHasChanged();

        try
        {
            var result = await LinkingApi.RunAutoLinkAsync();
            if (result.Success)
            {
                SuccessMessage = $"Auto-Verknüpfung abgeschlossen: {result.LinkedCount} verknüpft, {result.MarkedUnlinkedCount} als extern markiert, {result.RemainingUnlinkedCount} offen.";
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Auto-link error: {ex.Message}");
        }
        finally
        {
            IsAutoLinking = false;
            StateHasChanged();

            // Auto-hide success message
            _ = Task.Delay(5000).ContinueWith(_ => 
            {
                SuccessMessage = null;
                InvokeAsync(StateHasChanged);
            });
        }
    }

    // Link Dialog
    private async Task OpenLinkDialog(UnlinkedTransactionDTO tx)
    {
        LinkDialogTransaction = tx;
        IsLinkDialogOpen = true;
        SelectedMatch = null;
        LinkReason = null;
        LinkError = null;
        PotentialMatches = new List<UnlinkedTransactionDTO>();

        IsLoadingMatches = true;
        StateHasChanged();

        try
        {
            // Load opposite type transactions for the same symbol
            var oppositeType = tx.IsSend ? "receive" : "send";
            PotentialMatches = await LinkingApi.GetUnlinkedAsync(oppositeType, tx.Symbol, 50, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading matches: {ex.Message}");
        }
        finally
        {
            IsLoadingMatches = false;
            StateHasChanged();
        }
    }

    private void CloseLinkDialog()
    {
        IsLinkDialogOpen = false;
        LinkDialogTransaction = null;
    }

    private void SelectMatch(UnlinkedTransactionDTO match)
    {
        SelectedMatch = match;
    }

    private async Task ConfirmLink()
    {
        if (LinkDialogTransaction == null || SelectedMatch == null)
            return;

        IsLinking = true;
        LinkError = null;
        StateHasChanged();

        try
        {
            var sendId = LinkDialogTransaction.IsSend ? LinkDialogTransaction.Id : SelectedMatch.Id;
            var receiveId = LinkDialogTransaction.IsReceive ? LinkDialogTransaction.Id : SelectedMatch.Id;

            var result = await LinkingApi.ManualLinkAsync(sendId, receiveId, LinkReason);
            
            if (result.Success)
            {
                SuccessMessage = "Transaktionen erfolgreich verknüpft!";
                CloseLinkDialog();
                await LoadDataAsync();
            }
            else
            {
                LinkError = result.Message;
            }
        }
        catch (Exception ex)
        {
            LinkError = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsLinking = false;
            StateHasChanged();
        }
    }

    // Mark External Dialog
    private void OpenMarkExternalDialog(UnlinkedTransactionDTO tx)
    {
        MarkExternalTransaction = tx;
        ExternalReason = "";
        MarkExternalError = null;
        IsMarkExternalDialogOpen = true;
    }

    private void CloseMarkExternalDialog()
    {
        IsMarkExternalDialogOpen = false;
        MarkExternalTransaction = null;
    }

    private async Task ConfirmMarkExternal()
    {
        if (MarkExternalTransaction == null || string.IsNullOrWhiteSpace(ExternalReason))
            return;

        IsMarkingExternal = true;
        MarkExternalError = null;
        StateHasChanged();

        try
        {
            var result = await LinkingApi.MarkAsIntentionallyUnlinkedAsync(MarkExternalTransaction.Id, ExternalReason);
            
            if (result.Success)
            {
                SuccessMessage = "Transaktion als extern markiert!";
                CloseMarkExternalDialog();
                await LoadDataAsync();
            }
            else
            {
                MarkExternalError = result.Message;
            }
        }
        catch (Exception ex)
        {
            MarkExternalError = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsMarkingExternal = false;
            StateHasChanged();
        }
    }

    // Reset Dialog
    private void OpenResetDialog()
    {
        ResetKeepManual = true;
        ResetIncludeExternal = false;
        IsResetDialogOpen = true;
    }

    private void CloseResetDialog()
    {
        IsResetDialogOpen = false;
    }

    private async Task ConfirmReset()
    {
        IsResetting = true;
        StateHasChanged();

        try
        {
            var result = await LinkingApi.ResetLinksAsync(ResetKeepManual, ResetIncludeExternal);
            SuccessMessage = $"Reset abgeschlossen: {result.ResetCount} Verknüpfungen zurückgesetzt.";
            CloseResetDialog();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Reset error: {ex.Message}");
        }
        finally
        {
            IsResetting = false;
            StateHasChanged();
        }
    }

    // Helpers
    private static string TruncateAddress(string address)
    {
        if (string.IsNullOrEmpty(address) || address.Length <= 16)
            return address;
        return $"{address[..8]}...{address[^6..]}";
    }

    private static string FormatMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // Simple markdown formatting
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*(.+?)\*", "<em>$1</em>");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"`(.+?)`", "<code>$1</code>");
        text = text.Replace("\n", "<br/>");

        return text;
    }
}
