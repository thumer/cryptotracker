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

        public void Import(Func<Stream> openStreamFunc)
        {
            using var stream = openStreamFunc();
            using TextReader reader = new StreamReader(stream);
            using CsvReader csvReader = new CsvReader(reader, CreateCsvConfiguration());
            var records = csvReader.GetRecords<T>();
            OnImport(records);
        }

        protected virtual CsvConfiguration CreateCsvConfiguration() 
        {
            return new CsvConfiguration(CultureInfo.InvariantCulture);
        }

        protected abstract void OnImport(IEnumerable<T> records);
    }
}
