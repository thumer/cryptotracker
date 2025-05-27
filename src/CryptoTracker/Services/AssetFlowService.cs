using CryptoTracker.Entities;
using CryptoTracker.Shared;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Services;

public class AssetFlowService
{
    private readonly CryptoTrackerDbContext _dbContext;

    public AssetFlowService(CryptoTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IList<AssetFlowLineDTO>> GetAssetFlows(string walletName, string symbol)
    {
        var trades = await _dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .Where(t => (t.Symbol == symbol || t.OppositeSymbol == symbol) && t.Wallet.Name == walletName)
            .ToListAsync();

        var transactions = await _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .Include(t => t.OppositeWallet)
            .Where(t => t.Symbol == symbol && (t.Wallet.Name == walletName || t.OppositeWallet!.Name == walletName))
            .ToListAsync();

        var result = new List<AssetFlowLineDTO>();

        foreach (var t in transactions)
        {
            var amount = t.TransactionType == TransactionType.Receive ? t.QuantityAfterFee : -t.Quantity;
            result.Add(new AssetFlowLineDTO(
                t.DateTime,
                t.TransactionId,
                null,
                t.TransactionType == TransactionType.Send ? t.Wallet.Name : t.OppositeWallet?.Name,
                t.TransactionType == TransactionType.Receive ? t.Wallet.Name : t.OppositeWallet?.Name,
                t.Symbol,
                amount,
                null,
                null,
                null));
        }

        foreach (var tr in trades)
        {
            if (tr.Symbol == symbol)
            {
                var amount = tr.TradeType == TradeType.Buy ? tr.QuantityAfterFee : -tr.Quantity;
                result.Add(new AssetFlowLineDTO(
                    tr.DateTime,
                    null,
                    tr.Id,
                    tr.Wallet.Name,
                    tr.Wallet.Name,
                    tr.Symbol,
                    amount,
                    tr.OppositeSymbol,
                    tr.TradeType == TradeType.Buy ? -tr.Quantity * tr.Price : tr.QuantityAfterFee * tr.Price,
                    tr.Price));
            }
            else if (tr.OppositeSymbol == symbol)
            {
                var amount = tr.TradeType == TradeType.Buy ? -(tr.Quantity * tr.Price) : tr.QuantityAfterFee * tr.Price;
                result.Add(new AssetFlowLineDTO(
                    tr.DateTime,
                    null,
                    tr.Id,
                    tr.Wallet.Name,
                    tr.Wallet.Name,
                    tr.OppositeSymbol,
                    amount,
                    tr.Symbol,
                    tr.TradeType == TradeType.Buy ? tr.QuantityAfterFee : -tr.Quantity,
                    tr.Price));
            }
        }

        return result.OrderBy(r => r.DateTime).ToList();
    }
}
