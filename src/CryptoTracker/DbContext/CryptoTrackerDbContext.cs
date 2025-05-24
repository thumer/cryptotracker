using CryptoTracker.Entities;
using CryptoTracker.Entities.Import;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker
{
    public class CryptoTrackerDbContext : DbContext
    {
        public DbSet<CryptoTransaction> CryptoTransactions { get; set; }
        public DbSet<CryptoTrade> CryptoTrades { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<BinanceDepositEntity> BinanceDeposits { get; set; }
        public DbSet<BinanceWithdrawalEntity> BinanceWithdrawals { get; set; }
        public DbSet<BinanceTradeEntity> BinanceTrades { get; set; }
        public DbSet<BitcoinDeTransactionEntity> BitcoinDeTransactions { get; set; }
        public DbSet<BitpandaTransactionEntity> BitpandaTransactions { get; set; }
        public DbSet<MetamaskTradeEntity> MetamaskTrades { get; set; }
        public DbSet<MetamaskTransactionEntity> MetamaskTransactions { get; set; }
        public DbSet<OkxDepositEntity> OkxDeposits { get; set; }
        public DbSet<OkxTradeEntity> OkxTrades { get; set; }

        public CryptoTrackerDbContext(DbContextOptions<CryptoTrackerDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Wallet>().HasKey(w => w.Id);
            modelBuilder.Entity<Wallet>().HasIndex(w => w.Name).IsUnique();

            modelBuilder.Entity<CryptoTransaction>().HasKey(c => c.Id);
            modelBuilder.Entity<CryptoTransaction>()
                .HasOne(c => c.OppositeTransaction)
                .WithOne()
                .HasForeignKey<CryptoTransaction>(c => c.OppositeTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<CryptoTransaction>()
                .HasOne(c => c.Wallet)
                .WithMany()
                .HasForeignKey(c => c.WalletId);
            modelBuilder.Entity<CryptoTransaction>()
                .HasOne(c => c.OppositeWallet)
                .WithMany()
                .HasForeignKey(c => c.OppositeWalletId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CryptoTrade>().HasKey(c => c.Id);
            modelBuilder.Entity<CryptoTrade>()
                .HasOne(c => c.OppositeTrade)
                .WithOne()
                .HasForeignKey<CryptoTrade>(c => c.OppositeTradeId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<CryptoTrade>()
                .HasOne(c => c.Wallet)
                .WithMany()
                .HasForeignKey(c => c.WalletId);

            modelBuilder.Entity<BinanceDepositEntity>().HasKey(b => b.Id);
            modelBuilder.Entity<BinanceWithdrawalEntity>().HasKey(b => b.Id);
            modelBuilder.Entity<BinanceTradeEntity>().HasKey(b => b.Id);
            modelBuilder.Entity<BitcoinDeTransactionEntity>().HasKey(b => b.Id);
            modelBuilder.Entity<BitpandaTransactionEntity>().HasKey(b => b.Id);
            modelBuilder.Entity<MetamaskTradeEntity>().HasKey(b => b.Id);
            modelBuilder.Entity<MetamaskTransactionEntity>().HasKey(b => b.Id);
            modelBuilder.Entity<OkxDepositEntity>().HasKey(b => b.Id);
            modelBuilder.Entity<OkxTradeEntity>().HasKey(b => b.Id);

            modelBuilder.Entity<CryptoTrade>()
                .Property(c => c.Price)
                .HasColumnType("decimal(27, 12)"); // Beispiel: 18 Gesamtanzahl Ziffern, 8 Dezimalstellen

            modelBuilder.Entity<CryptoTrade>()
                .Property(c => c.Quantity)
                .HasColumnType("decimal(27, 12)");

            modelBuilder.Entity<CryptoTrade>()
                .Property(c => c.Fee)
                .HasColumnType("decimal(27, 12)");

            modelBuilder.Entity<CryptoTrade>()
                .Property(c => c.ForeignFee)
                .HasColumnType("decimal(27, 12)");

            modelBuilder.Entity<CryptoTransaction>()
                .Property(c => c.Quantity)
                .HasColumnType("decimal(27, 12)");

            modelBuilder.Entity<CryptoTransaction>()
                .Property(c => c.Fee)
                .HasColumnType("decimal(27, 12)");

            base.OnModelCreating(modelBuilder);
        }
    }
}
