using CryptoTracker.Client.Pages;
using CryptoTracker.Entities;
using CryptoTracker.Import.Objects;
using CsvHelper;
using CsvHelper.Configuration;

namespace CryptoTracker.Import
{
    public class MetamaskTransactionImporter : ImporterBase<MetamaskTransaction>
    {
        public MetamaskTransactionImporter(CryptoTrackerDbContext dbContext)
            : base(dbContext)
        {
        }

        protected override CsvConfiguration CreateCsvConfiguration()
            => new CsvConfiguration(new System.Globalization.CultureInfo("de-AT"))
            {
                Delimiter = ";",
            };

        protected override void OnCsvReaderCreated(CsvReader reader)
        {
            base.OnCsvReaderCreated(reader);
            reader.Context.TypeConverterCache.AddConverter<DateTimeOffset>(new UtcDateTimeConverter());
        }

        protected override async Task OnImport(ImportArgs args, IEnumerable<MetamaskTransaction> records)
        {
            foreach (var record in records)
            {
                var transaction = new CryptoTransaction
                {
                    TransactionType = record.Typ == "Eingang" ? TransactionType.Receive : TransactionType.Send,
                    WalletId = args.Wallet.Id,
                    DateTime = record.Datum,
                    Symbol = record.Coin,
                    Quantity = record.Amount + record.TransactionFee,
                    Fee = record.TransactionFee,
                    Network = record.Network,
                    Comment = record.Kommentar
                };
                DbContext.Add(transaction);
            }

            await DbContext.SaveChangesAsync();
        }
    }
}