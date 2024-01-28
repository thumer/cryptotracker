using CsvHelper.Configuration.Attributes;

namespace CryptoTracker.Import.Objects
{

    // csv-format: ; as separator, "" for values, culture: de-AT for date, en-US for numbers, Datum in CET
    // Example:
    // Datum;Typ;Währung;Referenz;Adresse;Kurs;"Einheit (Kurs)";"Crypto vor Gebühr";"Menge vor Gebühr";"Einheit (Menge vor Gebühr)";"Crypto nach Bitcoin.de-Gebühr";"Menge nach Bitcoin.de-Gebühr";"Einheit (Menge nach Bitcoin.de-Gebühr)";"Zu- / Abgang";Kontostand;Kommentar
    // "2013-04-17 15:35:15";Kauf;BTC;QW3A9T;;61.00;"BTC / EUR";1.00000000;61.00;EUR;0.99000000;60.69;EUR;0.99000000;0.99000000;"Erster BTC Kauf"
    // "2013-04-17 20:55:35";Auszahlung;BTC;a0545e842dd311d998cc4797be7502f573e3d1a6cb5c6cf0a8713e64e28be63a;196pDut3qJFHY8qPRVagUjtmggCkZBBwBt;;;;;;;;;-0.99000000;0.00000000;PC Wallet

    public class BitcoinDeTransaction : ICryptoCsvEntry
    {
        public DateTime Datum { get; set; }

        /// <summary>
        /// Kauf, Verkauf, Auszahlung, Einzahlung, Netzwerk-Gebühr, Partnerprogramm, Korrekturposition, Registrierung, Initialisierung
        /// </summary>
        public string Typ {  get; set; }

        /// <summary>
        /// Symbol der Cryptowährung
        /// </summary>
        [Name("Währung")]
        public string Waehrung { get; set; }

        /// <summary>
        /// z.B. Transaction Hash bei Ein- oder Auszahlung oder Bitcoin.de TransactionId bei Kauf/Verkauf
        /// </summary>
        public string Referenz { get; set; }

        /// <summary>
        /// Ziel- oder Quelladdresse bei einer Crypto Transaktion
        /// </summary>
        public string Adresse { get; set; }

        /// <summary>
        /// Kurs in EUR (z.B. 61 EUR pro BTC)
        /// </summary>
        public decimal? Kurs { get; set; }

        /// <summary>
        /// z.B. BTC / EUR
        /// </summary>
        [Name("Einheit (Kurs)")]
        public string EinheitKurs { get; set; }

        /// <summary>
        /// z.B. Anzahl an BTC/ETH vor Gebührenabzug. z.B. 1.000
        /// </summary>
        [Name("Crypto vor Gebühr")]
        public decimal? CryptoVorGebuehr { get; set; }

        /// <summary>
        /// z.B. Anzahl der Gegenwährung (z.B. EUR) vor Gebührenabzug z.B. 61€
        /// </summary>
        [Name("Menge vor Gebühr")]
        public decimal? MengeVorGebuehr { get; set; }

        /// <summary>
        /// z.B. EUR
        /// </summary>
        [Name("Einheit (Menge vor Gebühr)")]
        public string EinheitMengeVorGebuehr { get; set; }

        /// <summary>
        /// z.B. Anzahl an BTC/ETH nach Gebührenabzug. z.B. 1.000
        /// </summary>
        [Name("Crypto nach Bitcoin.de-Gebühr")]
        public decimal? CryptoNachGebuehr { get; set; }

        /// <summary>
        /// z.B. Anzahl der Gegenwährung (z.B. EUR) nach Gebührenabzug z.B. 61€
        /// </summary>
        [Name("Menge nach Bitcoin.de-Gebühr")]
        public decimal? MengeNachGebuehr { get; set; }

        /// <summary>
        /// z.B. EUR
        /// </summary>
        [Name("Einheit (Menge nach Bitcoin.de-Gebühr)")]
        public string EinheitMengeNachGebuehr { get; set; }

        /// <summary>
        /// Menge, die vom Konto zu- oder abgebucht wurden.
        /// </summary>
        [Name("Zu- / Abgang")]
        public decimal ZuAbgang { get; set; }

        /// <summary>
        /// Kontostand nach dieser Transaktion
        /// </summary>
        public decimal Kontostand {  get; set; }

        public string Kommentar { get; set; }
    }
}
