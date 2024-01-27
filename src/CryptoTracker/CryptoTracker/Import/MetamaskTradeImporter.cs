using CryptoTracker.Client.Pages;
using CryptoTracker.Import.Objects;
using CsvHelper;

namespace CryptoTracker.Import
{
    public class MetamaskTradeImporter : ImporterBase<MetamaskTrade>
    {
        public MetamaskTradeImporter(CryptoTrackerDbContext dbContext) 
            : base(dbContext)
        {
        }

        protected override void OnImport(IEnumerable<MetamaskTrade> records)
        {
            throw new NotImplementedException();
        }
    }
}
