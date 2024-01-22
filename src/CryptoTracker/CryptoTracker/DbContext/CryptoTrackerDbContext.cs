using CryptoTracker.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker
{
    public class CryptoTrackerDbContext : DbContext
    {
        public DbSet<FiatTransaction> FiatTransactions { get; set; }
        public DbSet<CryptoTransaction> CryptoTransactions { get; set; }
        public DbSet<CryptoTrade> CryptoTrades { get; set; }

        public CryptoTrackerDbContext(DbContextOptions<CryptoTrackerDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FiatTransaction>().HasKey(f => f.Id);
            modelBuilder.Entity<CryptoTransaction>().HasKey(c => c.Id);
            modelBuilder.Entity<CryptoTrade>().HasKey(c => c.Id);

            modelBuilder.Entity<CryptoTransaction>()
                .Property(c => c.Quantity);

            modelBuilder.Entity<CryptoTransaction>()
                .Property(c => c.Wallet);

            modelBuilder.Entity<CryptoTransaction>()
                .Property(c => c.Address);

            modelBuilder.Entity<CryptoTransaction>()
                .Property(c => c.Fee);

            modelBuilder.Entity<CryptoTrade>()
                .Property(c => c.Quantity);

            modelBuilder.Entity<CryptoTrade>()
                .Property(c => c.Price);

            base.OnModelCreating(modelBuilder);
        }
    }
}
