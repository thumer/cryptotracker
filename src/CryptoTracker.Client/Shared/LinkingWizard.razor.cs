using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components;

namespace CryptoTracker.Client.Shared;

public partial class LinkingWizard
{
    [Parameter] public EventCallback OnComplete { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }
    [Parameter] public LinkingStatisticsDTO? Statistics { get; set; }
    
    [Inject] private ITransactionLinkingApi LinkingApi { get; set; } = default!;

    private int CurrentStep = 1;
    private const int TotalSteps = 4;
    private bool IsProcessing = false;
    private bool IsAutoLinking = false;
    private AutoLinkResultDTO? AutoLinkResult;
    private IList<UnlinkedTransactionDTO> RemainingUnlinked = new List<UnlinkedTransactionDTO>();
    private LinkingStatisticsDTO? FinalStatistics;

    private bool CanProceed => CurrentStep switch
    {
        1 => true,
        2 => AutoLinkResult != null,
        3 => true,
        4 => true,
        _ => false
    };

    private string GetStepLabel(int step) => step switch
    {
        1 => "Übersicht",
        2 => "Auto-Link",
        3 => "Prüfen",
        4 => "Fertig",
        _ => ""
    };

    private async Task NextStep()
    {
        if (CurrentStep == 2 && AutoLinkResult != null)
        {
            // Load remaining unlinked for step 3
            IsProcessing = true;
            try
            {
                RemainingUnlinked = await LinkingApi.GetUnlinkedAsync();
            }
            finally
            {
                IsProcessing = false;
            }
        }
        
        if (CurrentStep == 3)
        {
            // Load final statistics for step 4
            IsProcessing = true;
            try
            {
                FinalStatistics = await LinkingApi.GetStatisticsAsync();
            }
            finally
            {
                IsProcessing = false;
            }
        }

        if (CurrentStep < TotalSteps)
        {
            CurrentStep++;
        }
    }

    private void PreviousStep()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
        }
    }

    private async Task RunAutoLink()
    {
        IsAutoLinking = true;
        StateHasChanged();

        try
        {
            AutoLinkResult = await LinkingApi.RunAutoLinkAsync();
        }
        catch (Exception ex)
        {
            AutoLinkResult = new AutoLinkResultDTO
            {
                Success = false,
                Summary = $"Fehler: {ex.Message}"
            };
        }
        finally
        {
            IsAutoLinking = false;
            StateHasChanged();
        }
    }

    private async Task OnLinkTransaction(UnlinkedTransactionDTO tx)
    {
        // This would open a sub-dialog for linking
        // For now, we'll just refresh the list
        // The actual linking happens on the main page
        await Task.CompletedTask;
    }

    private async Task OnMarkExternal(UnlinkedTransactionDTO tx)
    {
        try
        {
            var reason = tx.IsReceive ? "Externe Einnahme (via Wizard)" : "Externe Ausgabe (via Wizard)";
            var result = await LinkingApi.MarkAsIntentionallyUnlinkedAsync(tx.Id, reason);
            
            if (result.Success)
            {
                // Remove from list
                var list = RemainingUnlinked.ToList();
                list.Remove(tx);
                RemainingUnlinked = list;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error marking as external: {ex.Message}");
        }
    }

    private async Task Complete()
    {
        await OnComplete.InvokeAsync();
    }
}
