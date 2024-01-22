namespace CryptoTracker.Services
{
    public class DataImportService
    {
        private readonly CryptoTrackerDbContext _dbContext;

        public DataImportService(CryptoTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void Import(string data)
        {
        }
    }
}