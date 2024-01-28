using CryptoTracker.Client.Pages;
using CryptoTracker.Import.Objects;
using CsvHelper;

namespace CryptoTracker.Import
{
    public class BinanceWithdrawalImporter : ImporterBase<BinanceWithdrawal>
    {
        public BinanceWithdrawalImporter(CryptoTrackerDbContext dbContext) 
            : base(dbContext)
        {
        }

        protected override async Task OnImport(ImportArgs args, IEnumerable<BinanceWithdrawal> records)
        {
            throw new NotImplementedException();
        }
    }
}
