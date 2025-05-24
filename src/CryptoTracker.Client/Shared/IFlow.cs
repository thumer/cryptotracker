namespace CryptoTracker.Shared;

public enum FlowDirection
{
    Inflow,
    Outflow
}

public enum FlowType
{
    Trade,
    Transaction
}

public interface IFlow
{
    FlowType FlowType { get; }
    DateTimeOffset DateTime { get; }
    string Symbol { get; }
    string? SourceWallet { get; }
    string? TargetWallet { get; }
    FlowDirection FlowDirection { get; }
    decimal FlowAmount { get; }
}
