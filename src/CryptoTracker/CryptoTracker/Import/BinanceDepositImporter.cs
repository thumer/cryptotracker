using CryptoTracker.Import.Objects;
using CsvHelper.Configuration;

namespace CryptoTracker.Import
{
    public class BinanceDepositImporter : ImporterBase<BinanceDeposit>
    {
        public BinanceDepositImporter(CryptoTrackerDbContext dbContext) 
            : base(dbContext)
        {
        }

        protected override CsvConfiguration CreateCsvConfiguration()
            => new CsvConfiguration(new System.Globalization.CultureInfo("de-AT"))
                    {
                        Delimiter = ";",
                    };

        protected override void OnImport(IEnumerable<BinanceDeposit> records)
        {
        }
    }
}
