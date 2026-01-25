namespace CryptoTracker.Shared;

public record ImportPreviewResult(bool Success,
                                  string? ErrorMessage,
                                  string? DetectedSource,
                                  ImportDocumentType? DocumentType,
                                  string? DocumentTypeDisplayName,
                                  IList<ImportPreviewTransactionRowDTO> TransactionRows,
                                  bool TransactionsTruncated,
                                  IList<ImportPreviewTradeRowDTO> TradeRows,
                                  bool TradesTruncated);
