using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase, ITransactionsApi
{
    private readonly TransactionService _transactionService;

    public TransactionsController(TransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    [HttpGet("GetTransactions")]
    public Task<IList<TransactionRowDTO>> GetTransactions([FromQuery] string? walletName, [FromQuery] string? symbol, [FromQuery] bool showHidden = false)
        => _transactionService.GetTransactionsAsync(walletName, symbol, showHidden);

    [HttpGet("GetTransactionDetails")]
    public Task<FlowDetailsDTO?> GetTransactionDetails([FromQuery] FlowType flowType, [FromQuery] int id, [FromQuery] bool showHidden = false)
        => _transactionService.GetTransactionDetailsAsync(flowType, id, showHidden);

    [HttpPost("SetHidden")]
    public Task<bool> SetHidden([FromBody] SetHiddenRequest request)
        => _transactionService.SetHiddenAsync(request.FlowType, request.Id, request.IsHidden);

    Task<IList<TransactionRowDTO>> ITransactionsApi.GetTransactionsAsync(string? walletName, string? symbol, bool showHidden)
        => _transactionService.GetTransactionsAsync(walletName, symbol, showHidden);

    Task<FlowDetailsDTO?> ITransactionsApi.GetTransactionDetailsAsync(FlowType flowType, int id, bool showHidden)
        => _transactionService.GetTransactionDetailsAsync(flowType, id, showHidden);

    Task<bool> ITransactionsApi.SetHiddenAsync(SetHiddenRequest request)
        => _transactionService.SetHiddenAsync(request.FlowType, request.Id, request.IsHidden);
}
