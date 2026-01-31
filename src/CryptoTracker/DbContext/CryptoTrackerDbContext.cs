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
        public DbSet<LedgerTransactionEntity> LedgerTransactions { get; set; }
        public DbSet<OkxDepositEntity> OkxDeposits { get; set; }
        public DbSet<OkxTradeEntity> OkxTrades { get; set; }
        public DbSet<ManualCoinPrice> ManualCoinPrices { get; set; }
        public DbSet<AssetLot> AssetLots { get; set; }
        public DbSet<LotMovement> LotMovements { get; set; }

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
            modelBuilder.Entity<LedgerTransactionEntity>().HasKey(b => b.Id);
            modelBuilder.Entity<OkxDepositEntity>().HasKey(b => b.Id);
            modelBuilder.Entity<OkxTradeEntity>().HasKey(b => b.Id);
            modelBuilder.Entity<ManualCoinPrice>().HasKey(p => p.Id);
            modelBuilder.Entity<ManualCoinPrice>()
                .HasIndex(p => new { p.Symbol, p.Date })
                .IsUnique();

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

            modelBuilder.Entity<ManualCoinPrice>()
                .Property(p => p.PriceEur)
                .HasColumnType("decimal(27, 12)");

            modelBuilder.Entity<ManualCoinPrice>()
                .Property(p => p.Date)
                .HasColumnType("date");

            // === AssetLot Configuration ===
            modelBuilder.Entity<AssetLot>().HasKey(l => l.Id);
            modelBuilder.Entity<AssetLot>()
                .HasOne(l => l.CurrentWallet)
                .WithMany()
                .HasForeignKey(l => l.CurrentWalletId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AssetLot>()
                .HasOne(l => l.SourceTransaction)
                .WithMany()
                .HasForeignKey(l => l.SourceTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AssetLot>()
                .HasOne(l => l.SourceTrade)
                .WithMany()
                .HasForeignKey(l => l.SourceTradeId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AssetLot>()
                .HasOne(l => l.ParentLot)
                .WithMany(l => l.ChildLots)
                .HasForeignKey(l => l.ParentLotId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AssetLot>()
                .HasOne(l => l.TransformedToLot)
                .WithMany(l => l.TransformedFromLots)
                .HasForeignKey(l => l.TransformedToLotId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AssetLot>()
                .HasIndex(l => new { l.CurrentWalletId, l.Symbol });
            modelBuilder.Entity<AssetLot>()
                .HasIndex(l => l.AcquisitionDate);
            modelBuilder.Entity<AssetLot>()
                .Property(l => l.RemainingQuantity)
                .HasColumnType("decimal(27, 12)");
            modelBuilder.Entity<AssetLot>()
                .Property(l => l.OriginalQuantity)
                .HasColumnType("decimal(27, 12)");
            modelBuilder.Entity<AssetLot>()
                .Property(l => l.AcquisitionPriceEur)
                .HasColumnType("decimal(27, 12)");
            modelBuilder.Entity<AssetLot>()
                .Property(l => l.TotalAcquisitionCostEur)
                .HasColumnType("decimal(27, 12)");

            // === LotMovement Configuration ===
            modelBuilder.Entity<LotMovement>().HasKey(m => m.Id);
            modelBuilder.Entity<LotMovement>()
                .HasOne(m => m.Lot)
                .WithMany(l => l.Movements)
                .HasForeignKey(m => m.LotId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<LotMovement>()
                .HasOne(m => m.Trade)
                .WithMany(t => t.LotMovements)
                .HasForeignKey(m => m.TradeId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<LotMovement>()
                .HasOne(m => m.Transaction)
                .WithMany(t => t.LotMovements)
                .HasForeignKey(m => m.TransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<LotMovement>()
                .HasOne(m => m.ResultingLot)
                .WithMany()
                .HasForeignKey(m => m.ResultingLotId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<LotMovement>()
                .HasIndex(m => m.DateTime);
            modelBuilder.Entity<LotMovement>()
                .Property(m => m.Quantity)
                .HasColumnType("decimal(27, 12)");
            modelBuilder.Entity<LotMovement>()
                .Property(m => m.SalePriceEur)
                .HasColumnType("decimal(27, 12)");
            modelBuilder.Entity<LotMovement>()
                .Property(m => m.RealizedGainEur)
                .HasColumnType("decimal(27, 12)");

            // === CryptoTransaction Lot References ===
            modelBuilder.Entity<CryptoTransaction>()
                .HasOne(t => t.ResultingLot)
                .WithMany()
                .HasForeignKey(t => t.ResultingLotId)
                .OnDelete(DeleteBehavior.Restrict);

            // === CryptoTrade Lot References ===
            modelBuilder.Entity<CryptoTrade>()
                .HasOne(t => t.ResultingLot)
                .WithMany()
                .HasForeignKey(t => t.ResultingLotId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<CryptoTrade>()
                .HasOne(t => t.SourceLot)
                .WithMany()
                .HasForeignKey(t => t.SourceLotId)
                .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(modelBuilder);
        }
    }
}
