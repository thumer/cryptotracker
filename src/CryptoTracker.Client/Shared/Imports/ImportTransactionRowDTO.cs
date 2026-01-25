namespace CryptoTracker.Shared;

public record ImportTransactionRowDTO(DateTimeOffset DateTime,
                                      string Type,
                                      string Symbol,
                                      decimal Amount,
                                      decimal Fee,
                                      string Wallet,
                                      string? Network,
                                      string? Address,
                                      string? Comment,
                                      string? TransactionId,
                                      Guid RowKey);
