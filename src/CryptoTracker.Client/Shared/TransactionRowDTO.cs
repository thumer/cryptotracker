namespace CryptoTracker.Shared;

public record TransactionRowDTO(FlowType FlowType,
                                FlowDirection FlowDirection,
                                DateTimeOffset DateTime,
                                string Symbol,
                                decimal Amount,
                                decimal EuroValue,
                                decimal RateEur,
                                string? SourceWallet,
                                string? TargetWallet,
                                string? Slug,
                                string? TargetSymbol,
                                decimal? TargetAmount,
                                string? TargetSlug,
                                Guid RowKey);
