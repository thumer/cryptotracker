namespace CryptoTracker.Shared;

public record ImportTradeRowDTO(DateTimeOffset DateTime,
                                string Side,
                                string Symbol,
                                string OppositeSymbol,
                                decimal Price,
                                decimal Quantity,
                                decimal Fee,
                                string Wallet,
                                string? Comment,
                                string? Referenz,
                                Guid RowKey);
