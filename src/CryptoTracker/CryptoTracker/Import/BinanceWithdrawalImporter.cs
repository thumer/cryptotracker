using CryptoTracker.Client.Pages;
using CryptoTracker.Entities;
using CryptoTracker.Import.Objects;
using CsvHelper;
using CsvHelper.Configuration;

namespace CryptoTracker.Import
{
    public class BinanceWithdrawalImporter : ImporterBase<BinanceWithdrawal>
    {
        public BinanceWithdrawalImporter(CryptoTrackerDbContext dbContext) 
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

        protected override async Task OnImport(ImportArgs args, IEnumerable<BinanceWithdrawal> records)
        {
            foreach (var record in records)
            {
                var transaction = new CryptoTransaction
                {
                    TransactionType = TransactionType.Send,
                    Wallet = args.Wallet,
                    DateTime = record.Date.LocalDateTime,
                    Symbol = record.Coin,
                    Quantity = record.Amount + record.TransactionFee,
                    Fee = record.TransactionFee,
                    TransactionId = record.TXID,
                    Address = record.Address,
                    Network = record.Network,
                    Comment = record.Comment
                };
                DbContext.Add(transaction);
            }

            await DbContext.SaveChangesAsync();
        }
    }
}
