using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components;

namespace CryptoTracker.Client.Pages;

public partial class LotsLinking
{
    [Inject] private ILotsApi LotsApi { get; set; } = default!;
    [Inject] private ITransactionLinkingApi LinkingApi { get; set; } = default!;

    // State
    private bool IsLoading = true;
    private bool IsValidating = false;
    private string ActiveTab = "pending";
    private string? SuccessMessage;

    // Data
    private FlowStatisticsDTO? FlowStatistics;
    private IList<PendingLotAssignmentDTO> PendingAssignments = new List<PendingLotAssignmentDTO>();
    private IList<LotDTO> IncompleteLots = new List<LotDTO>();

    // Flow View
    private int? SearchLotId;
    private LotFlowValidationDTO? SelectedLotFlow;

    // Assignment Dialog
    private bool IsAssignmentDialogOpen = false;
    private PendingLotAssignmentDTO? SelectedAssignment;
    private IList<LotDTO> AvailableLots = new List<LotDTO>();
    private List<LotAllocationDTO> LotAllocations = new();
    private bool IsLoadingAvailableLots = false;
    private bool IsAssigning = false;
    private string? AssignmentError;

    private decimal AllocationDifference => 
        SelectedAssignment != null 
            ? SelectedAssignment.Quantity - LotAllocations.Sum(a => a.Quantity) 
            : 0;

    // Create Lot Dialog
    private bool IsCreateLotDialogOpen = false;
    private PendingLotAssignmentDTO? CreateLotForAssignment;
    private string NewLotAcquisitionType = "ExternalDeposit";
    private DateTime NewLotAcquisitionDate = DateTime.Today;
    private decimal NewLotAcquisitionPrice = 0;
    private string? NewLotNote;
    private bool IsCreatingLot = false;
    private string? CreateLotError;

    // Flow Details Dialog
    private bool IsFlowDetailsOpen = false;
    private LotDTO? SelectedLotForFlow;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            var pendingTask = LotsApi.GetPendingLotAssignmentsAsync();
            
            // Load pending assignments
            PendingAssignments = await pendingTask;

            // Calculate statistics
            FlowStatistics = new FlowStatisticsDTO
            {
                TotalLots = 0, // Will be calculated from actual data
                CompleteFlows = 0,
                IncompleteFlows = IncompleteLots.Count,
                PendingAssignments = PendingAssignments.Count
            };
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

    private void SetActiveTab(string tab)
    {
        ActiveTab = tab;
    }

    private async Task ValidateAllFlows()
    {
        IsValidating = true;
        StateHasChanged();

        try
        {
            // This would call an API to revalidate all flows
            await Task.Delay(1000); // Placeholder
            SuccessMessage = "Flow-Validierung abgeschlossen";
            await LoadDataAsync();
        }
        finally
        {
            IsValidating = false;
            StateHasChanged();
            _ = HideSuccessMessage();
        }
    }

    // Assignment Dialog
    private async Task OpenAssignmentDialog(PendingLotAssignmentDTO assignment)
    {
        SelectedAssignment = assignment;
        IsAssignmentDialogOpen = true;
        LotAllocations.Clear();
        AssignmentError = null;
        AvailableLots = new List<LotDTO>();

        IsLoadingAvailableLots = true;
        StateHasChanged();

        try
        {
            // Load available lots for this symbol from the opposite wallet (for transfers)
            // For external receives, load from any wallet
            if (assignment.OppositeWalletName != null)
            {
                AvailableLots = await LotsApi.GetAvailableLotsAsync(assignment.OppositeWalletName, assignment.Symbol);
            }
            else
            {
                // No opposite wallet - this is an external receive, no lots to assign
                AvailableLots = new List<LotDTO>();
            }
        }
        catch (Exception ex)
        {
            AssignmentError = $"Fehler beim Laden der Lots: {ex.Message}";
        }
        finally
        {
            IsLoadingAvailableLots = false;
            StateHasChanged();
        }
    }

    private void CloseAssignmentDialog()
    {
        IsAssignmentDialogOpen = false;
        SelectedAssignment = null;
    }

    private void SetAllocation(int lotId, ChangeEventArgs e)
    {
        var value = decimal.TryParse(e.Value?.ToString(), out var qty) ? qty : 0;
        
        var existing = LotAllocations.FirstOrDefault(a => a.LotId == lotId);
        if (existing != null)
        {
            LotAllocations.Remove(existing);
        }
        
        if (value > 0)
        {
            LotAllocations.Add(new LotAllocationDTO(lotId, value));
        }
    }

    private async Task ConfirmAssignment()
    {
        if (SelectedAssignment == null || LotAllocations.Count == 0)
            return;

        IsAssigning = true;
        AssignmentError = null;
        StateHasChanged();

        try
        {
            // This would call the appropriate API based on type
            if (SelectedAssignment.Type == "Transaction")
            {
                // For transactions, we need the send/receive pair
                // This is a simplified version - real implementation needs more context
                await Task.Delay(500); // Placeholder
            }
            else if (SelectedAssignment.Type == "Trade")
            {
                // For trades (sells), use SellLotsAsync
                // await LotsApi.SellLotsAsync(new SellLotsRequest(...));
                await Task.Delay(500); // Placeholder
            }

            SuccessMessage = "Lot-Zuordnung erfolgreich!";
            CloseAssignmentDialog();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            AssignmentError = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsAssigning = false;
            StateHasChanged();
            _ = HideSuccessMessage();
        }
    }

    // Create Lot Dialog
    private void OpenCreateLotDialog(PendingLotAssignmentDTO assignment)
    {
        CreateLotForAssignment = assignment;
        IsCreateLotDialogOpen = true;
        NewLotAcquisitionType = "ExternalDeposit";
        NewLotAcquisitionDate = assignment.DateTime.Date;
        NewLotAcquisitionPrice = 0;
        NewLotNote = null;
        CreateLotError = null;
        
        // Close assignment dialog if open
        if (IsAssignmentDialogOpen)
        {
            IsAssignmentDialogOpen = false;
        }
    }

    private void CloseCreateLotDialog()
    {
        IsCreateLotDialogOpen = false;
        CreateLotForAssignment = null;
    }

    private async Task CreateLot()
    {
        if (CreateLotForAssignment == null)
            return;

        IsCreatingLot = true;
        CreateLotError = null;
        StateHasChanged();

        try
        {
            // Look up wallet ID from name (simplified - real implementation needs API)
            var request = new CreateManualLotRequest(
                Symbol: CreateLotForAssignment.Symbol,
                WalletId: 0, // Would need to resolve from wallet name
                Quantity: CreateLotForAssignment.Quantity,
                AcquisitionDate: new DateTimeOffset(NewLotAcquisitionDate, TimeSpan.Zero),
                AcquisitionPriceEur: NewLotAcquisitionPrice,
                AcquisitionType: NewLotAcquisitionType,
                SourceTransactionId: CreateLotForAssignment.Type == "Transaction" ? CreateLotForAssignment.Id : null,
                Note: NewLotNote
            );

            await LotsApi.CreateManualLotAsync(request);
            
            SuccessMessage = "Lot erfolgreich erstellt!";
            CloseCreateLotDialog();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            CreateLotError = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsCreatingLot = false;
            StateHasChanged();
            _ = HideSuccessMessage();
        }
    }

    // Flow Details
    private async Task OpenFlowDetails(LotDTO lot)
    {
        SelectedLotForFlow = lot;
        IsFlowDetailsOpen = true;
        SelectedLotFlow = null;
        StateHasChanged();

        try
        {
            // Load flow for this lot
            // SelectedLotFlow = await LotsApi.GetLotFlowAsync(lot.Id);
            await Task.Delay(500); // Placeholder
            
            // Create mock data for now
            SelectedLotFlow = new LotFlowValidationDTO(
                LotId: lot.Id,
                Symbol: lot.Symbol,
                Quantity: lot.OriginalQuantity,
                AcquisitionDate: lot.AcquisitionDate,
                IsComplete: lot.IsFlowComplete,
                IncompleteReason: lot.FlowIncompleteReason,
                FlowChain: new List<LotFlowStepDTO>
                {
                    new LotFlowStepDTO(lot.Id, lot.Symbol, lot.OriginalQuantity, lot.AcquisitionType, lot.SourceTradeId, lot.SourceTransactionId, lot.AcquisitionDate)
                },
                EffectiveQuantity: lot.RemainingQuantity
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading flow: {ex.Message}");
        }
        
        StateHasChanged();
    }

    private void CloseFlowDetails()
    {
        IsFlowDetailsOpen = false;
        SelectedLotForFlow = null;
        SelectedLotFlow = null;
    }

    private async Task LoadLotFlow()
    {
        if (SearchLotId == null)
            return;

        try
        {
            var lot = await LotsApi.GetLotByIdAsync(SearchLotId.Value);
            if (lot != null)
            {
                await OpenFlowDetails(lot);
                IsFlowDetailsOpen = false; // Show inline instead
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading lot: {ex.Message}");
        }
    }

    private async Task HideSuccessMessage()
    {
        await Task.Delay(5000);
        SuccessMessage = null;
        await InvokeAsync(StateHasChanged);
    }
}

/// <summary>
/// Statistics for flow completeness
/// </summary>
public record FlowStatisticsDTO
{
    public int TotalLots { get; init; }
    public int CompleteFlows { get; init; }
    public int IncompleteFlows { get; init; }
    public int PendingAssignments { get; init; }
}
