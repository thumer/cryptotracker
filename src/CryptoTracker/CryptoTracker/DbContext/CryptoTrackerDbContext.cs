using CryptoTracker.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker
{
    public class CryptoTrackerDbContext : DbContext
    {
        public DbSet<CryptoTransaction> CryptoTransactions { get; set; }
        public DbSet<CryptoTrade> CryptoTrades { get; set; }

        public CryptoTrackerDbContext(DbContextOptions<CryptoTrackerDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CryptoTransaction>().HasKey(c => c.Id);
            modelBuilder.Entity<CryptoTrade>().HasKey(c => c.Id);

            base.OnModelCreating(modelBuilder);
        }
    }
}
