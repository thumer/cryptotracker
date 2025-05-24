namespace CryptoTracker.Shared;

public record AssetBalanceDTO(string Symbol, decimal Amount, decimal EuroValue);
public record PlatformBalanceDTO(string Platform, IList<AssetBalanceDTO> Assets);
