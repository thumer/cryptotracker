using CryptoTracker.Client.Pages;
using CryptoTracker.Import.Objects;
using CsvHelper;

namespace CryptoTracker.Import
{
    public class OkxDepositImporter : ImporterBase<OkxDeposit>
    {
        public OkxDepositImporter(CryptoTrackerDbContext dbContext) 
            : base(dbContext)
        {
        }

        protected override void OnImport(IEnumerable<OkxDeposit> records)
        {
            throw new NotImplementedException();
        }
    }
}
