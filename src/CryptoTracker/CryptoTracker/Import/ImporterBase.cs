using CryptoTracker.Import.Objects;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace CryptoTracker.Import
{
    public abstract class ImporterBase<T> : IImporter
        where T : ICryptoCsvEntry
    {
        public ImporterBase(CryptoTrackerDbContext dbContext) 
        {
            DbContext = dbContext;
        }

        protected CryptoTrackerDbContext DbContext { get; }

        public async Task Import(ImportArgs args, Func<Stream> openStreamFunc)
        {
            using var stream = openStreamFunc();
            using TextReader reader = new StreamReader(stream);
            using CsvReader csvReader = new CsvReader(reader, CreateCsvConfiguration());
            var records = csvReader.GetRecords<T>();
            await OnImport(args, records);
        }

        protected virtual CsvConfiguration CreateCsvConfiguration() 
        {
            return new CsvConfiguration(CultureInfo.InvariantCulture);
        }

        protected abstract Task OnImport(ImportArgs args, IEnumerable<T> records);
    }
}
