namespace CryptoTracker.Shared;

public record TradeDetailsDTO(DateTimeOffset DateTime,
                              FlowDirection FlowDirection,
                              string Symbol,
                              decimal Amount,
                              decimal EuroValue,
                              string? Slug,
                              string? TargetSymbol,
                              decimal? TargetAmount,
                              string? TargetSlug,
                              string Wallet,
                              decimal Fee,
                              string FeeSymbol,
                              decimal ForeignFee,
                              string? ForeignFeeSymbol,
                              string? Referenz,
                              string? Comment);

public record TransactionDetailsDTO(DateTimeOffset DateTime,
                                    FlowDirection FlowDirection,
                                    string Symbol,
                                    decimal Amount,
                                    decimal EuroValue,
                                    string? Slug,
                                    string? SourceWallet,
                                    string? TargetWallet,
                                    decimal Fee,
                                    string FeeSymbol,
                                    string? Comment,
                                    string? TransactionId,
                                    string? Address,
                                    string? Network);

public record FlowDetailsDTO(FlowType FlowType,
                             TradeDetailsDTO? Trade,
                             TradeDetailsDTO? OppositeTrade,
                             TransactionDetailsDTO? Transaction,
                             TransactionDetailsDTO? OppositeTransaction);
