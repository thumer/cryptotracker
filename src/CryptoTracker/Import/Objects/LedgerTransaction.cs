using CsvHelper.Configuration.Attributes;

namespace CryptoTracker.Import.Objects;

// csv-format: ; as separator, no "" for values, culture: de-AT (numbers, date), date in UTC
// Example:
// Datum (UTC);Typ;Coin;Network;Address;Amount;TransactionFee;Kommentar
// 02.07.2021 20:38;Eingang;ETH;BSC;0xabc...;0,209932;0,000068;von binance.com
public class LedgerTransaction : ICryptoCsvEntry
{
    /// <summary>
    /// Datum in ISO 8601 und UTC
    /// </summary>
    [Name("Datum (UTC)", "Datum")]
    public DateTimeOffset Datum { get; set; }

    /// <summary>
    /// Eingang, Ausgang
    /// </summary>
    public string Typ { get; set; } = string.Empty;

    /// <summary>
    /// Symbol der Cryptowährung
    /// </summary>
    public string Coin { get; set; } = string.Empty;

    /// <summary>
    /// Netzwerk über das die Coins übertragen wurden
    /// </summary>
    public string Network { get; set; } = string.Empty;

    /// <summary>
    /// Ziel- oder Quelladresse bei einer Cryptotransaktion
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Anzahl der Coins vor Gebührenabzug
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Preis die ausbezahlt wurden. Entspricht Preis nach Fee Abzug.
    /// </summary>
    public decimal TransactionFee { get; set; }

    public string Kommentar { get; set; } = string.Empty;
}
