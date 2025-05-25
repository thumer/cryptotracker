using System.Text.Json;
using CryptoTracker.Shared;

namespace CryptoTracker.Common;

public static class ImportEntryExtensions
{
    public static TDto CloneToDTO<TDto>(this object entity)
    {
        var json = JsonSerializer.Serialize(entity);
        var dto = JsonSerializer.Deserialize<TDto>(json)!;

        var walletPropEntity = entity.GetType().GetProperty("Wallet");
        var walletPropDto = typeof(TDto).GetProperty("Wallet");
        if (walletPropEntity != null && walletPropDto != null)
        {
            var wallet = walletPropEntity.GetValue(entity);
            var nameProp = wallet?.GetType().GetProperty("Name");
            var walletName = nameProp?.GetValue(wallet)?.ToString() ?? string.Empty;
            walletPropDto.SetValue(dto, walletName);
        }
        return dto;
    }
}
