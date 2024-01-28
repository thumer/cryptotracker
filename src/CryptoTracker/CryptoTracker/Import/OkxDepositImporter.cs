using CryptoTracker.Entities;
using CryptoTracker.Import.Objects;
using CsvHelper;
using CsvHelper.Configuration;

namespace CryptoTracker.Import
{
    public class OkxDepositImporter : ImporterBase<OkxDeposit>
    {
        public OkxDepositImporter(CryptoTrackerDbContext dbContext) 
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

        protected override async Task OnImport(ImportArgs args, IEnumerable<OkxDeposit> records)
        {
            foreach (var record in records)
            {
                var transaction = new CryptoTransaction
                {
                    TransactionType = TransactionType.Receive,
                    Wallet = args.Wallet,
                    DateTime = record.Date.LocalDateTime,
                    Symbol = record.Coin,
                    Quantity = record.Amount,
                    Fee = 0,
                    Network = record.Network,
                    Comment = record.Kommentar
                };
                DbContext.Add(transaction);
            }

            await DbContext.SaveChangesAsync();
        }
    }
}
