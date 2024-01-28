using CryptoTracker.Client.Pages;
using CryptoTracker.Import.Objects;
using CsvHelper;

namespace CryptoTracker.Import
{
    public class BinanceTradeImporter : ImporterBase<BinanceTrade>
    {
        public BinanceTradeImporter(CryptoTrackerDbContext dbContext) 
            : base(dbContext)
        {
        }

        protected override async Task OnImport(ImportArgs args, IEnumerable<BinanceTrade> records)
        {
            throw new NotImplementedException();
        }
    }
}
