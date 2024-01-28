using CryptoTracker.Client.Pages;
using CryptoTracker.Import.Objects;
using CsvHelper;

namespace CryptoTracker.Import
{
    public class BitpandaTransactionImporter : ImporterBase<BitpandaTransaction>
    {
        public BitpandaTransactionImporter(CryptoTrackerDbContext dbContext) 
            : base(dbContext)
        {
        }

        protected override async Task OnImport(ImportArgs args, IEnumerable<BitpandaTransaction> records)
        {
            throw new NotImplementedException();
        }
    }
}
