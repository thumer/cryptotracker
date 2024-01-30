namespace CryptoTracker.Entities
{
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
        DateTime DateTime { get; set; }
        string Symbol { get; set; }
        string? SourceWallet { get; }
        string? TargetWallet { get; }
        FlowDirection FlowDirection { get; }
        decimal FlowAmount { get; }
    }
}
