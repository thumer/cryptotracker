using CryptoTracker.Entities;
using CryptoTracker.Import.Objects;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace CryptoTracker.Import;

public class LedgerTransactionImporter : ImporterBase<LedgerTransaction>
{
    public LedgerTransactionImporter(CryptoTrackerDbContext dbContext)
        : base(dbContext)
    {
    }

    protected override CsvConfiguration CreateCsvConfiguration()
        => new CsvConfiguration(new CultureInfo("de-AT"))
        {
            Delimiter = ";",
        };

    protected override void OnCsvReaderCreated(CsvReader reader)
    {
        base.OnCsvReaderCreated(reader);
        reader.Context.TypeConverterCache.AddConverter<DateTimeOffset>(new UtcDateTimeConverter());
    }

    protected override async Task OnImport(ImportArgs args, IEnumerable<LedgerTransaction> records)
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
                Address = record.Address,
                Comment = record.Kommentar
            };
            DbContext.Add(transaction);
        }

        await DbContext.SaveChangesAsync();
    }
}
