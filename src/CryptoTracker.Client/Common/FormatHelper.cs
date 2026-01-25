using System.Globalization;

namespace CryptoTracker.Client.Common;

public static class FormatHelper
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("de-DE");

    public static string FormatAmount(decimal value)
        => value.ToString("0.00########", Culture);

    public static string FormatEuro(decimal value)
        => $"{FormatAmount(value)} €";

    public static string FormatEuroNullable(decimal? value)
        => value.HasValue ? FormatEuro(value.Value) : "—";

    public static string FormatUtc(DateTimeOffset value)
        => value.ToUniversalTime().ToString("dd.MM.yyyy HH:mm:ss", Culture);

    public static string FormatDate(DateTime value)
        => value.ToString("dd.MM.yyyy", Culture);
}
