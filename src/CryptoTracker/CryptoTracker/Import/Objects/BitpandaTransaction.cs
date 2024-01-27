using CsvHelper.Configuration.Attributes;
using DevExpress.Pdf.Native.BouncyCastle.Asn1.X509;

namespace CryptoTracker.Import.Objects
{

    // csv-format: , as separator, "" for values, date im ISO 8601-Standard, en-US for numbers, Datum in CET
    // Example:
    // "Transaction ID",Timestamp,"Transaction Type",In/Out,"Amount Fiat",Fiat,"Amount Asset",Asset,"Asset market price","Asset market price currency","Asset class","Product ID",Fee,"Fee asset",Spread,"Spread Currency","Tax Fiat"
    // Fabb6de9a-fe34-45b4-9d4b-79e6b08fa29d,2022-01-03T12:56:40+01:00,deposit,incoming,100.00,EUR,-,EUR,-,-,Fiat,-,0.00000000,EUR,-,-,0.00
    // Tf99fd4b9-97d2-4665-80f3-db45e6dfceab,2022-01-29T16:21:30+01:00,buy,outgoing,100.00,EUR,0.04245838,ETH,2355.25,EUR,Cryptocurrency,5,-,-,-,-,0.00
    // C2ca8a52b-3672-4ced-954f-ad2929be66f9,2022-03-21T18:56:17+01:00,withdrawal,outgoing,0,EUR,0.38188785,ETH,0.00,-,Cryptocurrency,5,0.00297774,ETH,-,-,-,0x8be99316f151a4cf393e46371020e5079d7f6d91,Juicyfields

    public class BitpandaTransaction
    {
        /// <summary>
        /// Interne TransactionId
        /// </summary>
        [Name("Transaction ID")]
        public string TransactionId { get; set; }

        /// <summary>
        /// Datum in ISO 8601 und CET
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// deposit, withdrawal, buy, sell
        /// </summary>
        [Name("Transaction Type")]
        public string TransactionType {  get; set; }

        /// <summary>
        /// incoming, outgoing
        /// </summary>
        [Name("In/Out")]
        public string InOut { get; set; }

        /// <summary>
        /// Betrag in Fiat für Kauf oder Verkauf aber auch den aktuellen Wert bei Crypto Aus-/Einzahlung.
        /// </summary>
        [Name("Amount Fiat")]
        public double AmountFiat { get; set; }

        /// <summary>
        /// Symbol der Fiat Währung
        /// </summary>
        public string Fiat { get; set; }

        /// <summary>
        /// Menge an Cryptos vor Gebührenabzug bei Transaktion
        /// </summary>
        [Name("Amount Asset")]
        public double AmountAsset { get; set; }

        /// <summary>
        /// Symbol der Asset Währung
        /// </summary>
        public string Asset { get; set; }

        /// <summary>
        /// Aktuelle Preis der Cryptowährung in der {AssetMarketPriceCurrency} Währung (in der Regel Fiat Währung).
        /// </summary>
        [Name("Asset market price")]
        public double AssetMarketPrice { get; set; }

        [Name("Asset market price currency")]
        public string AssetMarketPriceCurrency { get; set; }

        /// <summary>
        /// Asset Class z.B. Cryptocurrency
        /// </summary>
        [Name("Asset class")]
        public string AssetClass { get; set; }

        [Name("Product ID")]
        public int? ProductID { get; set; }

        /// <summary>
        /// Gebühr bei einer withdrawal Transaktion in der {FeeAsset} Währung.
        /// </summary>
        public double Fee { get; set; }

        [Name("Fee asset")]
        public double FeeAsset { get; set; }

        public double Spread { get; set; }

        [Name("Spread Currency")]
        public double SpreadCurrency { get; set; }

        [Name("Tax Fiat")]
        public double TaxFiat { get; set; }

        /// <summary>
        /// Ziel- oder Quell-Hash Addresse bei einer Cryptotransaktion
        /// </summary>
        public string Address { get; set; }

        public string Comment { get; set; }
    }
}
