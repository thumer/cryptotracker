using System.Text.Json.Serialization;

namespace CryptoTracker.Shared;

public record BitcoinDeTransactionDTO
{
    public int WalletId { get; set; }
    public string Wallet { get; set; } = string.Empty;

    public DateTimeOffset Datum { get; set; }
    public string Typ { get; set; } = string.Empty;
    public string Waehrung { get; set; } = string.Empty;
    public string Referenz { get; set; } = string.Empty;
    public string Adresse { get; set; } = string.Empty;
    public decimal? Kurs { get; set; }
    public string EinheitKurs { get; set; } = string.Empty;
    public decimal? CryptoVorGebuehr { get; set; }
    public decimal? MengeVorGebuehr { get; set; }
    public string EinheitMengeVorGebuehr { get; set; } = string.Empty;
    public decimal? CryptoNachGebuehr { get; set; }
    public decimal? MengeNachGebuehr { get; set; }
    public string EinheitMengeNachGebuehr { get; set; } = string.Empty;
    public decimal ZuAbgang { get; set; }
    public decimal Kontostand { get; set; }
    public string Kommentar { get; set; } = string.Empty;
}
