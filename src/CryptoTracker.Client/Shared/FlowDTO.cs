namespace CryptoTracker.Shared;

public record FlowDTO(FlowType FlowType,
                      DateTimeOffset DateTime, 
                      string Symbol, 
                      string? SourceWallet, 
                      string? TargetWallet, 
                      FlowDirection FlowDirection, 
                      decimal FlowAmount) : IFlow;