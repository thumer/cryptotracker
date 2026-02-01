using System.Linq;

namespace CryptoTracker.Services;

internal static class FiatSymbols
{
    // Enthält auch Lowercase-Varianten für EF Core (case-sensitiv).
    public static readonly string[] ForQuery =
    {
        "EUR", "USD", "CHF", "GBP", "ZEUR", "ZUSD",
        "eur", "usd", "chf", "gbp", "zeur", "zusd"
    };

    public static bool IsFiat(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return false;
        }

        return ForQuery.Contains(symbol, StringComparer.OrdinalIgnoreCase);
    }
}
