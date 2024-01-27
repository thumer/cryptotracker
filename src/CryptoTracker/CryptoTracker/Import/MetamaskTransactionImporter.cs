using CryptoTracker.Client.Pages;
using CryptoTracker.Import.Objects;
using CsvHelper;

namespace CryptoTracker.Import
{
    public class MetamaskTransactionImporter : ImporterBase<MetamaskTransaction>
    {
        public MetamaskTransactionImporter(CryptoTrackerDbContext dbContext) 
            : base(dbContext)
        {
        }

        protected override void OnImport(IEnumerable<MetamaskTransaction> records)
        {
            throw new NotImplementedException();
        }
    }
}
