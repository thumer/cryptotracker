namespace CryptoTracker.Shared;

public record SetHiddenRequest(FlowType FlowType, int Id, bool IsHidden);
