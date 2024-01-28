using CryptoTracker.Client.Pages;
using CryptoTracker.Import.Objects;
using CsvHelper;

namespace CryptoTracker.Import
{
    public class BitcoinDeTransactionImporter : ImporterBase<BitcoinDeTransaction>
    {
        public BitcoinDeTransactionImporter(CryptoTrackerDbContext dbContext) 
            : base(dbContext)
        {
        }

        protected override async Task OnImport(ImportArgs args, IEnumerable<BitcoinDeTransaction> records)
        {
            throw new NotImplementedException();
        }
    }
}
