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
    public Task<IList<TransactionRowDTO>> GetTransactions([FromQuery] string? walletName, [FromQuery] string? symbol)
        => _transactionService.GetTransactionsAsync(walletName, symbol);

    [HttpGet("GetTransactionDetails")]
    public Task<FlowDetailsDTO?> GetTransactionDetails([FromQuery] FlowType flowType, [FromQuery] int id)
        => _transactionService.GetTransactionDetailsAsync(flowType, id);

    Task<IList<TransactionRowDTO>> ITransactionsApi.GetTransactionsAsync(string? walletName, string? symbol)
        => _transactionService.GetTransactionsAsync(walletName, symbol);

    Task<FlowDetailsDTO?> ITransactionsApi.GetTransactionDetailsAsync(FlowType flowType, int id)
        => _transactionService.GetTransactionDetailsAsync(flowType, id);
}
