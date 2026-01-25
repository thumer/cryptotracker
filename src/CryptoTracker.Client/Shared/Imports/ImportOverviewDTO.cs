namespace CryptoTracker.Shared;

public record ImportOverviewDTO(IList<ImportTransactionRowDTO> Deposits,
                                IList<ImportTransactionRowDTO> Withdrawals,
                                IList<ImportTradeRowDTO> Trades);
