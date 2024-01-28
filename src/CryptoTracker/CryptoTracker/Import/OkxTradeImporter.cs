using CryptoTracker.Client.Pages;
using CryptoTracker.Import.Objects;
using CsvHelper;

namespace CryptoTracker.Import
{
    public class OkxTradeImporter : ImporterBase<OkxTrade>
    {
        public OkxTradeImporter(CryptoTrackerDbContext dbContext) 
            : base(dbContext)
        {
        }

        protected override async Task OnImport(ImportArgs args, IEnumerable<OkxTrade> records)
        {
            throw new NotImplementedException();
        }
    }
}
