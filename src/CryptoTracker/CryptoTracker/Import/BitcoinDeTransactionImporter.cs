using CryptoTracker.Entities;
using CryptoTracker.Import.Objects;
using CsvHelper.Configuration;
using System.Globalization;

namespace CryptoTracker.Import
{
    public class BitcoinDeTransactionImporter : ImporterBase<BitcoinDeTransaction>
    {
        public BitcoinDeTransactionImporter(CryptoTrackerDbContext dbContext) 
            : base(dbContext)
        {
        }

        protected override CsvConfiguration CreateCsvConfiguration()
            => new CsvConfiguration(new CultureInfo("en-US"))
            {
                Delimiter = ";"
            };

        protected override async Task OnImport(ImportArgs args, IEnumerable<BitcoinDeTransaction> records)
        {
            foreach (var record in records)
            {
                if (record.Typ == "Kauf" || record.Typ == "Verkauf")
                {
                    var sellTrade = new CryptoTrade
                    {
                        Wallet = args.Wallet,
                        DateTime = record.Datum,
                        Symbol = record.Typ == "Kauf" ? record.EinheitMengeVorGebuehr : record.Waehrung,
                        OpositeSymbol = record.Typ == "Kauf" ? record.Waehrung : record.EinheitMengeVorGebuehr,
                        TradeType = TradeType.Sell,
                        Price = record.Typ == "Kauf" ? 1 / record.Kurs!.Value : record.Kurs!.Value,
                        Quantity = record.Typ == "Kauf" ? record.MengeVorGebuehr!.Value : record.CryptoVorGebuehr!.Value,
                        Fee = record.Typ == "Kauf" ? record.MengeVorGebuehr!.Value - record.MengeNachGebuehr!.Value : 0,
                        ForeignFee = 0,
                        ForeignFeeSymbol = string.Empty,
                    };
                    var buyTrade = new CryptoTrade
                    {
                        Wallet = args.Wallet,
                        DateTime = record.Datum,
                        Symbol = record.Typ == "Kauf" ? record.Waehrung : record.EinheitMengeVorGebuehr,
                        OpositeSymbol = record.Typ == "Kauf" ? record.EinheitMengeVorGebuehr : record.Waehrung,
                        TradeType = TradeType.Buy,
                        Price = record.Typ == "Verkauf" ? 1 / record.Kurs!.Value : record.Kurs!.Value,
                        Quantity = record.Typ == "Kauf" ? 1 / record.CryptoVorGebuehr!.Value : record.MengeVorGebuehr!.Value,
                        Fee = record.Typ == "Verkauf" ? record.MengeVorGebuehr!.Value - record.MengeNachGebuehr!.Value : 0,
                        ForeignFee = 0,
                        ForeignFeeSymbol = string.Empty,
                    };

                    sellTrade.OppositeTrade = buyTrade;
                    buyTrade.OppositeTrade = sellTrade;
                    DbContext.Add(sellTrade);
                    DbContext.Add(buyTrade);
                }
                else if (record.Typ == "Einzahlung" || record.Typ == "Auszahlung")
                {
                    var transaction = new CryptoTransaction
                    {
                        TransactionType = record.Typ == "Einzahlung" ? TransactionType.Receive : TransactionType.Send,
                        Wallet = args.Wallet,
                        DateTime = record.Datum,
                        Symbol = record.Waehrung,
                        Quantity = record.Typ == "Auszahlung" ? record.ZuAbgang * -1 : record.ZuAbgang,
                        Fee = 0,
                        TransactionId = record.Referenz,
                        Address = record.Adresse,
                        Comment = record.Kommentar
                    };
                    DbContext.Add(transaction);
                }
                else if (record.Typ == "Netzwerk-Gebühr")
                {
                    var cryptoTransaction = DbContext.CryptoTransactions.Local.Where(t => t.TransactionId == record.Referenz).FirstOrDefault();
                    if (cryptoTransaction != null)
                    {
                        cryptoTransaction.Fee += record.ZuAbgang * -1;
                        cryptoTransaction.Quantity += cryptoTransaction.Fee;
                    }
                }
            }

            await DbContext.SaveChangesAsync();
        }
    }
}