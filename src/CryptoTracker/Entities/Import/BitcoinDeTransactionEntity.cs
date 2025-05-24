namespace CryptoTracker.Entities.Import;

public class BitcoinDeTransactionEntity
{
    public int Id { get; set; }
    public int WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;

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
