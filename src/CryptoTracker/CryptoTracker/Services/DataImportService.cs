using CryptoTracker.Entities;
using CryptoTracker.Import;
using CryptoTracker.Shared;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Services
{
    public class DataImportService
    {
        private readonly CryptoTrackerDbContext _dbContext;

        public DataImportService(CryptoTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task Import(ImportDocumentType type, string walletName, Func<Stream> openStreamFunc)
        {
            var importer = GetImporter(type);
            await importer.Import(new ImportArgs() { Wallet = walletName }, openStreamFunc);
        }

        public async Task ProcessTransactionPairs()
        {
            var transactions = await _dbContext.CryptoTransactions.ToListAsync();

            foreach (var transaction in transactions.Where(t => t.TransactionType == TransactionType.Send))
            {
                var oppositeTransaction = transactions
                    .Where(t => t.TransactionType == TransactionType.Receive &&
                                t.QuantityAfterFee == transaction.QuantityAfterFee &&
                                t.DateTime >= transaction.DateTime.AddMinutes(-1) /* die Sekunden müssen nicht immer stimmen */ &&
                                t.DateTime <= transaction.DateTime.AddHours(5))
                    .FirstOrDefault();

                if (oppositeTransaction != null)
                {
                    transaction.OppositeTransaction = oppositeTransaction;
                    transaction.OppositeWallet = oppositeTransaction.Wallet;
                    oppositeTransaction.OppositeTransaction = transaction;
                    oppositeTransaction.OppositeWallet = transaction.Wallet;
                }
            }

            await _dbContext.SaveChangesAsync();
        }

        private IImporter GetImporter(ImportDocumentType type)
            => type switch
            {
                ImportDocumentType.BinanceDepositHistory => new BinanceDepositImporter(_dbContext),
                ImportDocumentType.BinanceTradingHistory => new BinanceTradeImporter(_dbContext),
                ImportDocumentType.BinanceWithdrawalHistory => new BinanceWithdrawalImporter(_dbContext),
                ImportDocumentType.BitcoinDeTransactions => new BitcoinDeTransactionImporter(_dbContext),
                ImportDocumentType.BitpandaTransaction => new BitpandaTransactionImporter(_dbContext),
                ImportDocumentType.MetamaskTradingHistory => new MetamaskTradeImporter(_dbContext),
                ImportDocumentType.MetamaskTransactions => new MetamaskTransactionImporter(_dbContext),
                ImportDocumentType.OkxDepositHistory => new OkxDepositImporter(_dbContext),
                ImportDocumentType.OkxTradingHistory => new OkxTradeImporter(_dbContext),
                _ => throw new NotSupportedException()
            };
    }
}
