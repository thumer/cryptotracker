using CryptoTracker.Agent.Common;
using CryptoTracker.Entities;
using CryptoTracker.Services;
using CryptoTracker.Shared;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;

namespace CryptoTracker.Agent.Tools;

/// <summary>
/// Tool um Root-Lots zu erstellen (Fiat-Kauf oder externe Einzahlung).
/// </summary>
public sealed class CreateRootLotTool : IAgentTool
{
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly LotService _lotService;
    private readonly CoinRateService _coinRateService;
    private readonly ILotLinkingAgentContextAccessor _contextAccessor;

    public CreateRootLotTool(
        CryptoTrackerDbContext dbContext,
        LotService lotService,
        CoinRateService coinRateService,
        ILotLinkingAgentContextAccessor contextAccessor)
    {
        _dbContext = dbContext;
        _lotService = lotService;
        _coinRateService = coinRateService;
        _contextAccessor = contextAccessor;
    }

    public Delegate GetToolRunner() => CreateRootLotAsync;
    public string GetToolName() => "create_root_lot";
    public string GetToolDescription() => """
        Erstellt ein Root-Lot aus Fiat-Kauf oder externer Einzahlung.
        Parameter:
        - sourceType: "trade" oder "transaction"
        - sourceId: ID des Trades/der Transaktion
        - acquisitionType: FiatPurchase, ExternalDeposit, Mining, Staking, Airdrop, Gift, Manual
        - acquisitionPriceEur: Optional, wenn bekannt
        - note: Optional
        """;

    private async Task<string> CreateRootLotAsync(
        [Description("Quelle: trade/transaction")] string sourceType,
        [Description("ID der Quelle")] int sourceId,
        [Description("AcquisitionType")] string acquisitionType,
        [Description("Optional: Preis in EUR")] decimal? acquisitionPriceEur = null,
        [Description("Optional: Notiz")] string? note = null)
    {
        if (!Enum.TryParse<LotAcquisitionType>(acquisitionType, true, out var lotType))
        {
            return JsonSerializer.Serialize(new { success = false, error = $"Ungültiger acquisitionType: {acquisitionType}" });
        }

        AssetLot lot;
        if (sourceType.Equals("trade", StringComparison.OrdinalIgnoreCase))
        {
            var trade = await _dbContext.CryptoTrades
                .Include(t => t.Wallet)
                .FirstOrDefaultAsync(t => t.Id == sourceId);

            if (trade == null)
                return JsonSerializer.Serialize(new { success = false, error = $"Trade {sourceId} nicht gefunden" });

            if (trade.TradeType != TradeType.Buy)
                return JsonSerializer.Serialize(new { success = false, error = "Trade ist kein Buy" });

            var eurPrice = acquisitionPriceEur ?? await GuessEurPriceForTradeAsync(trade);
            lot = await _lotService.CreateLotFromFiatPurchaseAsync(trade, eurPrice);
        }
        else if (sourceType.Equals("transaction", StringComparison.OrdinalIgnoreCase))
        {
            var tx = await _dbContext.CryptoTransactions
                .Include(t => t.Wallet)
                .FirstOrDefaultAsync(t => t.Id == sourceId);

            if (tx == null)
                return JsonSerializer.Serialize(new { success = false, error = $"Transaktion {sourceId} nicht gefunden" });

            var eurPrice = acquisitionPriceEur ?? await GuessEurPriceForSymbolAsync(tx.Symbol, tx.DateTime.UtcDateTime);

            lot = await _lotService.CreateManualLotAsync(new ManualLotRequest
            {
                Symbol = tx.Symbol,
                WalletId = tx.WalletId,
                Quantity = tx.QuantityAfterFee,
                AcquisitionDate = tx.DateTime,
                AcquisitionPriceEur = eurPrice,
                AcquisitionType = lotType,
                SourceTransactionId = tx.Id,
                Note = note
            });
        }
        else
        {
            return JsonSerializer.Serialize(new { success = false, error = "sourceType muss 'trade' oder 'transaction' sein" });
        }

        var context = _contextAccessor.Current;
        if (context != null)
        {
            context.Session.CreatedLotsCount++;
            context.Session.AssignedCount++;
            context.Session.ProcessedCount = context.Session.AssignedCount + context.Session.SkippedCount;

            await context.SendEventAsync(new LotLinkingEventDTO
            {
                EventType = "lot_created",
                Message = $"Root-Lot erstellt: #{lot.Id} {lot.Symbol} {lot.OriginalQuantity:F8}",
                CreatedLot = MapToDto(lot),
                ProcessedCount = context.Session.ProcessedCount,
                TotalCount = context.Session.TotalCount
            });
        }

        return JsonSerializer.Serialize(new { success = true, lotId = lot.Id });
    }

    private async Task<decimal> GuessEurPriceForTradeAsync(CryptoTrade trade)
    {
        if (trade.OppositeSymbol.Equals("EUR", StringComparison.OrdinalIgnoreCase))
        {
            return trade.Price;
        }

        var (fiatRate, _) = await _coinRateService.GetPreviousCloseRateWithSourceAsync(
            trade.OppositeSymbol,
            trade.DateTime.UtcDateTime);
        if (fiatRate.HasValue && fiatRate.Value > 0)
        {
            return trade.Price * fiatRate.Value;
        }

        var cryptoRate = await GuessEurPriceForSymbolAsync(trade.Symbol, trade.DateTime.UtcDateTime);
        return cryptoRate > 0 ? cryptoRate : trade.Price;
    }

    private async Task<decimal> GuessEurPriceForSymbolAsync(string symbol, DateTime dateUtc)
    {
        var (rate, _) = await _coinRateService.GetPreviousCloseRateWithSourceAsync(symbol, dateUtc);
        return rate ?? 0m;
    }

    private static LotDTO MapToDto(AssetLot lot) => new(
        lot.Id,
        lot.Symbol,
        lot.CurrentWalletId,
        lot.CurrentWallet?.Name ?? string.Empty,
        lot.RemainingQuantity,
        lot.OriginalQuantity,
        lot.AcquisitionDate,
        lot.AcquisitionPriceEur,
        lot.TotalAcquisitionCostEur,
        lot.AcquisitionType.ToString(),
        lot.IsAltbestand,
        lot.IsFullyConsumed,
        lot.Note,
        lot.ParentLotId,
        lot.SourceTradeId,
        lot.SourceTransactionId,
        lot.IsFlowComplete,
        lot.FlowIncompleteReason);
}
