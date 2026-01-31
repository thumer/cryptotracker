using CryptoTracker.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CryptoTracker.Services;

/// <summary>
/// Service für Asset-Lot-Management (Tranchen-Tracking für Steuerberechnung).
/// Unterstützt feingranulare Zuordnung von Lots zu Transaktionen/Trades
/// sowie optionales Auto-FIFO für schnellere Workflows.
/// </summary>
public class LotService
{
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly CoinRateService _coinRateService;
    private readonly ILogger<LotService> _logger;

    public LotService(
        CryptoTrackerDbContext dbContext,
        CoinRateService coinRateService,
        ILogger<LotService> logger)
    {
        _dbContext = dbContext;
        _coinRateService = coinRateService;
        _logger = logger;
    }

    #region Lot-Abfragen

    /// <summary>
    /// Holt alle verfügbaren (nicht vollständig verbrauchten) Lots für ein Wallet und Symbol.
    /// Sortiert nach FIFO (älteste zuerst).
    /// </summary>
    /// <param name="walletId">Wallet ID</param>
    /// <param name="symbol">Asset-Symbol</param>
    /// <param name="onlyCompleteFlow">Nur Lots mit vollständigem Flow zurückgeben</param>
    public async Task<IList<AssetLot>> GetAvailableLotsAsync(int walletId, string symbol, bool onlyCompleteFlow = false)
    {
        var query = _dbContext.AssetLots
            .AsNoTracking()
            .Include(l => l.CurrentWallet)
            .Where(l => l.CurrentWalletId == walletId
                     && l.Symbol == symbol.ToUpperInvariant()
                     && l.RemainingQuantity > 0);

        if (onlyCompleteFlow)
        {
            query = query.Where(l => l.IsFlowComplete);
        }

        return await query
            .OrderBy(l => l.AcquisitionDate)
            .ToListAsync();
    }

    /// <summary>
    /// Holt alle verfügbaren Lots für ein Wallet und Symbol (inkl. Flow-Status).
    /// Gibt auch Lots mit unvollständigem Flow zurück, markiert diese aber entsprechend.
    /// </summary>
    public async Task<IList<AssetLot>> GetAllAvailableLotsWithFlowStatusAsync(int walletId, string symbol)
    {
        return await _dbContext.AssetLots
            .AsNoTracking()
            .Include(l => l.CurrentWallet)
            .Where(l => l.CurrentWalletId == walletId
                     && l.Symbol == symbol.ToUpperInvariant()
                     && l.RemainingQuantity > 0)
            .OrderBy(l => l.AcquisitionDate)
            .ToListAsync();
    }

    /// <summary>
    /// Holt alle verfügbaren Altbestand-Lots (vor 28.02.2021) für ein Wallet und Symbol.
    /// </summary>
    public async Task<IList<AssetLot>> GetAltbestandLotsAsync(int walletId, string symbol, bool onlyCompleteFlow = false)
    {
        var query = _dbContext.AssetLots
            .AsNoTracking()
            .Include(l => l.CurrentWallet)
            .Where(l => l.CurrentWalletId == walletId
                     && l.Symbol == symbol.ToUpperInvariant()
                     && l.RemainingQuantity > 0
                     && l.AcquisitionDate <= AssetLot.AltbestandStichtag);

        if (onlyCompleteFlow)
        {
            query = query.Where(l => l.IsFlowComplete);
        }

        return await query.OrderBy(l => l.AcquisitionDate).ToListAsync();
    }

    /// <summary>
    /// Holt alle verfügbaren Neubestand-Lots (ab 01.03.2021) für ein Wallet und Symbol.
    /// </summary>
    public async Task<IList<AssetLot>> GetNeubestandLotsAsync(int walletId, string symbol, bool onlyCompleteFlow = false)
    {
        var query = _dbContext.AssetLots
            .AsNoTracking()
            .Include(l => l.CurrentWallet)
            .Where(l => l.CurrentWalletId == walletId
                     && l.Symbol == symbol.ToUpperInvariant()
                     && l.RemainingQuantity > 0
                     && l.AcquisitionDate > AssetLot.AltbestandStichtag);

        if (onlyCompleteFlow)
        {
            query = query.Where(l => l.IsFlowComplete);
        }

        return await query.OrderBy(l => l.AcquisitionDate).ToListAsync();
    }

    /// <summary>
    /// Holt ein Lot anhand seiner ID.
    /// </summary>
    public async Task<AssetLot?> GetLotByIdAsync(int lotId)
    {
        return await _dbContext.AssetLots
            .Include(l => l.CurrentWallet)
            .Include(l => l.ParentLot)
            .Include(l => l.SourceTrade)
            .Include(l => l.SourceTransaction)
            .FirstOrDefaultAsync(l => l.Id == lotId);
    }

    /// <summary>
    /// Berechnet den gesamten verfügbaren Bestand pro Symbol auf einem Wallet.
    /// </summary>
    public async Task<Dictionary<string, LotSummary>> GetLotSummaryByWalletAsync(int walletId)
    {
        var lots = await _dbContext.AssetLots
            .AsNoTracking()
            .Where(l => l.CurrentWalletId == walletId && l.RemainingQuantity > 0)
            .ToListAsync();

        return lots
            .GroupBy(l => l.Symbol)
            .ToDictionary(
                g => g.Key,
                g => new LotSummary
                {
                    Symbol = g.Key,
                    TotalQuantity = g.Sum(l => l.RemainingQuantity),
                    AltbestandQuantity = g.Where(l => l.IsAltbestand).Sum(l => l.RemainingQuantity),
                    NeubestandQuantity = g.Where(l => !l.IsAltbestand).Sum(l => l.RemainingQuantity),
                    TotalAcquisitionCostEur = g.Sum(l => l.RemainingAcquisitionCostEur),
                    LotCount = g.Count()
                });
    }

    #endregion

    #region Lot-Erstellung

    /// <summary>
    /// Erstellt ein Lot aus einem Fiat-Kauf (Trade vom Typ Buy mit Fiat).
    /// </summary>
    public async Task<AssetLot> CreateLotFromFiatPurchaseAsync(CryptoTrade trade, decimal eurPrice)
    {
        if (trade.TradeType != TradeType.Buy)
            throw new ArgumentException("Trade muss vom Typ Buy sein", nameof(trade));

        var lot = new AssetLot
        {
            Symbol = trade.Symbol.ToUpperInvariant(),
            CurrentWalletId = trade.WalletId,
            RemainingQuantity = trade.QuantityAfterFee,
            OriginalQuantity = trade.QuantityAfterFee,
            AcquisitionDate = trade.DateTime,
            AcquisitionPriceEur = eurPrice,
            TotalAcquisitionCostEur = eurPrice * trade.QuantityAfterFee,
            AcquisitionType = LotAcquisitionType.FiatPurchase,
            SourceTradeId = trade.Id
        };

        _dbContext.AssetLots.Add(lot);
        await _dbContext.SaveChangesAsync();

        // Trade mit Lot verknüpfen
        trade.ResultingLotId = lot.Id;
        trade.LotAssignmentConfirmed = true;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Lot #{LotId} erstellt: {Quantity} {Symbol} @ {Price}€ (Fiat-Kauf, Trade #{TradeId})",
            lot.Id, lot.OriginalQuantity, lot.Symbol, lot.AcquisitionPriceEur, trade.Id);

        return lot;
    }

    /// <summary>
    /// Erstellt ein manuelles Lot (z.B. für Altbestand-Import oder externe Einzahlungen).
    /// </summary>
    public async Task<AssetLot> CreateManualLotAsync(ManualLotRequest request)
    {
        var lot = new AssetLot
        {
            Symbol = request.Symbol.ToUpperInvariant(),
            CurrentWalletId = request.WalletId,
            RemainingQuantity = request.Quantity,
            OriginalQuantity = request.Quantity,
            AcquisitionDate = request.AcquisitionDate,
            AcquisitionPriceEur = request.AcquisitionPriceEur,
            TotalAcquisitionCostEur = request.AcquisitionPriceEur * request.Quantity,
            AcquisitionType = request.AcquisitionType,
            SourceTransactionId = request.SourceTransactionId,
            Note = request.Note
        };

        _dbContext.AssetLots.Add(lot);
        await _dbContext.SaveChangesAsync();

        // Falls eine Transaktion verknüpft ist, diese als zugeordnet markieren
        if (request.SourceTransactionId.HasValue)
        {
            var transaction = await _dbContext.CryptoTransactions
                .FindAsync(request.SourceTransactionId.Value);
            if (transaction != null)
            {
                transaction.ResultingLotId = lot.Id;
                transaction.LotAssignmentConfirmed = true;
                await _dbContext.SaveChangesAsync();
            }
        }

        _logger.LogInformation(
            "Manuelles Lot #{LotId} erstellt: {Quantity} {Symbol} @ {Price}€ ({Type})",
            lot.Id, lot.OriginalQuantity, lot.Symbol, lot.AcquisitionPriceEur, lot.AcquisitionType);

        return lot;
    }

    #endregion

    #region Lot-Verwendung (Transfer, Verkauf)

    /// <summary>
    /// Führt einen Transfer von Lots zu einem anderen Wallet durch.
    /// Erstellt LotMovements und neue Lots auf dem Ziel-Wallet.
    /// </summary>
    public async Task<IList<AssetLot>> TransferLotsAsync(
        int sendTransactionId,
        int receiveTransactionId,
        IList<LotAllocation> allocations)
    {
        var sendTransaction = await _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.Id == sendTransactionId)
            ?? throw new ArgumentException($"Send-Transaktion {sendTransactionId} nicht gefunden");

        var receiveTransaction = await _dbContext.CryptoTransactions
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.Id == receiveTransactionId)
            ?? throw new ArgumentException($"Receive-Transaktion {receiveTransactionId} nicht gefunden");

        var resultingLots = new List<AssetLot>();

        foreach (var allocation in allocations)
        {
            var lot = await _dbContext.AssetLots.FindAsync(allocation.LotId)
                ?? throw new ArgumentException($"Lot {allocation.LotId} nicht gefunden");

            if (lot.RemainingQuantity < allocation.Quantity)
                throw new InvalidOperationException(
                    $"Lot #{lot.Id} hat nur {lot.RemainingQuantity} verfügbar, aber {allocation.Quantity} angefordert");

            // Lot reduzieren
            lot.RemainingQuantity -= allocation.Quantity;

            // Neues Lot auf Ziel-Wallet erstellen
            var newLot = new AssetLot
            {
                Symbol = lot.Symbol,
                CurrentWalletId = receiveTransaction.WalletId,
                RemainingQuantity = allocation.Quantity,
                OriginalQuantity = allocation.Quantity,
                AcquisitionDate = lot.AcquisitionDate, // Kaufdatum beibehalten!
                AcquisitionPriceEur = lot.AcquisitionPriceEur,
                TotalAcquisitionCostEur = lot.AcquisitionPriceEur * allocation.Quantity,
                AcquisitionType = LotAcquisitionType.InternalTransfer,
                SourceTransactionId = receiveTransactionId,
                ParentLotId = lot.Id,
                Note = $"Transfer von {sendTransaction.Wallet.Name}"
            };

            _dbContext.AssetLots.Add(newLot);
            await _dbContext.SaveChangesAsync();

            // Movement erstellen
            var movement = new LotMovement
            {
                LotId = lot.Id,
                Quantity = allocation.Quantity,
                DateTime = sendTransaction.DateTime,
                MovementType = LotMovementType.Transfer,
                TransactionId = sendTransactionId,
                IsTaxFree = true,
                TaxFreeReason = TaxFreeReason.InternalTransfer,
                ResultingLotId = newLot.Id
            };

            _dbContext.LotMovements.Add(movement);
            resultingLots.Add(newLot);
        }

        // Transaktionen als zugeordnet markieren
        sendTransaction.LotAssignmentConfirmed = true;
        receiveTransaction.LotAssignmentConfirmed = true;
        receiveTransaction.ResultingLotId = resultingLots.FirstOrDefault()?.Id;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Transfer abgeschlossen: {Count} Lots von Wallet #{FromWallet} zu #{ToWallet}",
            allocations.Count, sendTransaction.WalletId, receiveTransaction.WalletId);

        return resultingLots;
    }

    /// <summary>
    /// Führt einen Verkauf gegen Fiat durch und berechnet Gewinn/Verlust.
    /// </summary>
    public async Task<SaleResult> SellLotsAsync(
        int tradeId,
        IList<LotAllocation> allocations,
        decimal salePriceEurPerUnit)
    {
        var trade = await _dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.Id == tradeId)
            ?? throw new ArgumentException($"Trade {tradeId} nicht gefunden");

        if (trade.TradeType != TradeType.Sell)
            throw new ArgumentException("Trade muss vom Typ Sell sein");

        var result = new SaleResult();

        foreach (var allocation in allocations)
        {
            var lot = await _dbContext.AssetLots.FindAsync(allocation.LotId)
                ?? throw new ArgumentException($"Lot {allocation.LotId} nicht gefunden");

            if (lot.RemainingQuantity < allocation.Quantity)
                throw new InvalidOperationException(
                    $"Lot #{lot.Id} hat nur {lot.RemainingQuantity} verfügbar, aber {allocation.Quantity} angefordert");

            // Gewinn/Verlust berechnen
            var acquisitionCost = lot.AcquisitionPriceEur * allocation.Quantity;
            var saleProceeds = salePriceEurPerUnit * allocation.Quantity;
            var realizedGain = saleProceeds - acquisitionCost;
            var isTaxFree = lot.IsAltbestand;

            // Lot reduzieren
            lot.RemainingQuantity -= allocation.Quantity;

            // Movement erstellen
            var movement = new LotMovement
            {
                LotId = lot.Id,
                Quantity = allocation.Quantity,
                DateTime = trade.DateTime,
                MovementType = LotMovementType.FiatSale,
                TradeId = tradeId,
                SalePriceEur = salePriceEurPerUnit,
                RealizedGainEur = realizedGain,
                IsTaxFree = isTaxFree,
                TaxFreeReason = isTaxFree ? TaxFreeReason.Altbestand : null
            };

            _dbContext.LotMovements.Add(movement);

            // Ergebnis aggregieren
            result.TotalQuantity += allocation.Quantity;
            result.TotalAcquisitionCost += acquisitionCost;
            result.TotalSaleProceeds += saleProceeds;
            result.TotalRealizedGain += realizedGain;

            if (isTaxFree)
            {
                result.TaxFreeGain += realizedGain;
                result.TaxFreeQuantity += allocation.Quantity;
            }
            else
            {
                result.TaxableGain += realizedGain;
                result.TaxableQuantity += allocation.Quantity;
            }
        }

        // KESt berechnen (27,5% auf steuerpflichtigen Gewinn)
        if (result.TaxableGain > 0)
        {
            result.EstimatedKESt = result.TaxableGain * 0.275m;
        }

        // Trade als zugeordnet markieren
        trade.LotAssignmentConfirmed = true;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Verkauf abgeschlossen: {Quantity} {Symbol}, Gewinn: {Gain}€ (steuerfrei: {TaxFree}€, steuerpflichtig: {Taxable}€)",
            result.TotalQuantity, trade.Symbol, result.TotalRealizedGain, result.TaxFreeGain, result.TaxableGain);

        return result;
    }

    #endregion

    #region Swap-Transformation

    /// <summary>
    /// Transformiert Lots durch einen Crypto-zu-Crypto-Swap.
    /// Erstellt neue Lots für das Ziel-Asset und verknüpft sie mit den Quell-Lots.
    /// </summary>
    /// <param name="sellTradeId">Die Sell-Seite des Swaps (gibt Crypto ab)</param>
    /// <param name="buyTradeId">Die Buy-Seite des Swaps (erhält Crypto)</param>
    /// <param name="sourceAllocations">Lot-Zuordnungen für die Quell-Coins</param>
    /// <param name="resultingQuantity">Menge der erhaltenen Coins (nach Gebühren)</param>
    public async Task<AssetLot> TransformLotsViaSwapAsync(
        int sellTradeId,
        int buyTradeId,
        IList<LotAllocation> sourceAllocations,
        decimal resultingQuantity)
    {
        var sellTrade = await _dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.Id == sellTradeId)
            ?? throw new ArgumentException($"Sell-Trade {sellTradeId} nicht gefunden");

        var buyTrade = await _dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.Id == buyTradeId)
            ?? throw new ArgumentException($"Buy-Trade {buyTradeId} nicht gefunden");

        if (sellTrade.TradeType != TradeType.Sell)
            throw new ArgumentException("sellTradeId muss auf einen Sell-Trade verweisen");
        if (buyTrade.TradeType != TradeType.Buy)
            throw new ArgumentException("buyTradeId muss auf einen Buy-Trade verweisen");

        // Prüfen ob Trades ein Swap-Paar sind
        if (sellTrade.OppositeTradeId != buyTradeId || buyTrade.OppositeTradeId != sellTradeId)
        {
            throw new InvalidOperationException(
                "Die Trades sind kein gültiges Swap-Paar (OppositeTradeId stimmt nicht überein)");
        }

        // Berechne gewichteten durchschnittlichen Anschaffungspreis der Quell-Lots
        decimal totalSourceQuantity = 0;
        decimal totalSourceCost = 0;
        DateTimeOffset earliestAcquisitionDate = DateTimeOffset.MaxValue;
        var sourceLotIds = new List<int>();

        foreach (var allocation in sourceAllocations)
        {
            var lot = await _dbContext.AssetLots.FindAsync(allocation.LotId)
                ?? throw new ArgumentException($"Lot {allocation.LotId} nicht gefunden");

            if (lot.RemainingQuantity < allocation.Quantity)
                throw new InvalidOperationException(
                    $"Lot #{lot.Id} hat nur {lot.RemainingQuantity} verfügbar, aber {allocation.Quantity} angefordert");

            // Quell-Lot reduzieren
            lot.RemainingQuantity -= allocation.Quantity;

            totalSourceQuantity += allocation.Quantity;
            totalSourceCost += lot.AcquisitionPriceEur * allocation.Quantity;
            
            if (lot.AcquisitionDate < earliestAcquisitionDate)
            {
                earliestAcquisitionDate = lot.AcquisitionDate;
            }

            sourceLotIds.Add(lot.Id);

            // Movement erstellen für den Swap-Out
            var movement = new LotMovement
            {
                LotId = lot.Id,
                Quantity = allocation.Quantity,
                DateTime = sellTrade.DateTime,
                MovementType = LotMovementType.CryptoSwapOut,
                TradeId = sellTradeId,
                IsTaxFree = true, // Krypto-zu-Krypto Swaps sind in Österreich steuerfrei (Tausch)
                TaxFreeReason = TaxFreeReason.CryptoSwap
            };
            _dbContext.LotMovements.Add(movement);
        }

        // Neues Lot für das Ziel-Asset erstellen
        // Anschaffungskosten werden proportional übertragen
        var acquisitionPricePerUnit = totalSourceQuantity > 0 
            ? totalSourceCost / resultingQuantity 
            : 0;

        var newLot = new AssetLot
        {
            Symbol = buyTrade.Symbol.ToUpperInvariant(),
            CurrentWalletId = buyTrade.WalletId,
            RemainingQuantity = resultingQuantity,
            OriginalQuantity = resultingQuantity,
            // WICHTIG: Kaufdatum des ältesten Quell-Lots übernehmen (für Altbestand-Berechnung)
            AcquisitionDate = earliestAcquisitionDate,
            AcquisitionPriceEur = acquisitionPricePerUnit,
            TotalAcquisitionCostEur = totalSourceCost,
            AcquisitionType = LotAcquisitionType.CryptoSwap,
            SourceTradeId = buyTradeId,
            // Bei mehreren Quell-Lots: ParentLot auf das erste setzen, TransformedFromLots für alle
            ParentLotId = sourceLotIds.FirstOrDefault(),
            Note = $"Swap von {totalSourceQuantity} {sellTrade.Symbol}"
        };

        _dbContext.AssetLots.Add(newLot);
        await _dbContext.SaveChangesAsync();

        // Quell-Lots mit TransformedToLotId verknüpfen
        foreach (var lotId in sourceLotIds)
        {
            var sourceLot = await _dbContext.AssetLots.FindAsync(lotId);
            if (sourceLot != null)
            {
                sourceLot.TransformedToLotId = newLot.Id;
            }
        }

        // Bewegungen mit neuem Lot verknüpfen
        var movements = await _dbContext.LotMovements
            .Where(m => m.TradeId == sellTradeId && m.ResultingLotId == null)
            .ToListAsync();
        foreach (var movement in movements)
        {
            movement.ResultingLotId = newLot.Id;
        }

        // Trades als zugeordnet markieren
        sellTrade.LotAssignmentConfirmed = true;
        sellTrade.SourceLotId = sourceLotIds.FirstOrDefault();
        
        buyTrade.LotAssignmentConfirmed = true;
        buyTrade.ResultingLotId = newLot.Id;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Swap-Transformation abgeschlossen: {SourceQty} {SourceSymbol} -> {TargetQty} {TargetSymbol} (Lot #{NewLotId})",
            totalSourceQuantity, sellTrade.Symbol, resultingQuantity, buyTrade.Symbol, newLot.Id);

        return newLot;
    }

    #endregion

    #region Auto-FIFO

    /// <summary>
    /// Schlägt eine FIFO-Zuordnung für eine bestimmte Menge vor.
    /// Priorisiert Altbestand (steuerfrei) wenn gewünscht.
    /// </summary>
    public async Task<IList<LotAllocation>> SuggestFifoAllocationAsync(
        int walletId,
        string symbol,
        decimal requiredQuantity,
        bool prioritizeAltbestand = false)
    {
        var lots = await GetAvailableLotsAsync(walletId, symbol);

        if (prioritizeAltbestand)
        {
            // Altbestand zuerst, dann Neubestand
            lots = lots
                .OrderByDescending(l => l.IsAltbestand)
                .ThenBy(l => l.AcquisitionDate)
                .ToList();
        }

        var allocations = new List<LotAllocation>();
        var remaining = requiredQuantity;

        foreach (var lot in lots)
        {
            if (remaining <= 0) break;

            var toAllocate = Math.Min(lot.RemainingQuantity, remaining);
            allocations.Add(new LotAllocation
            {
                LotId = lot.Id,
                Quantity = toAllocate,
                Lot = lot
            });
            remaining -= toAllocate;
        }

        if (remaining > 0)
        {
            _logger.LogWarning(
                "FIFO-Vorschlag unvollständig: {Missing} {Symbol} fehlen auf Wallet #{WalletId}",
                remaining, symbol, walletId);
        }

        return allocations;
    }

    #endregion

    #region Lot-Generierung aus bestehenden Daten

    /// <summary>
    /// Fiat-Symbole für EF Core Query (muss als statisches Array definiert sein).
    /// Enthält auch Lowercase-Varianten für Case-Insensitive-Vergleich.
    /// </summary>
    private static readonly string[] FiatSymbolsForQuery = { "EUR", "USD", "CHF", "GBP", "ZEUR", "ZUSD", "eur", "usd", "chf", "gbp", "zeur", "zusd" };

    /// <summary>
    /// Generiert Lots aus bestehenden Buy-Trades die noch kein Lot haben.
    /// </summary>
    public async Task<int> GenerateLotsFromExistingTradesAsync()
    {
        // Hinweis: FiatSymbolsForQuery muss inline verwendet werden, da EF Core keine Methodenaufrufe übersetzen kann
        // Contains-Vergleich ist case-sensitive, daher enthält das Array beide Varianten
        var tradesWithoutLots = await _dbContext.CryptoTrades
            .Include(t => t.Wallet)
            .Where(t => t.TradeType == TradeType.Buy
                     && t.ResultingLotId == null
                     && FiatSymbolsForQuery.Contains(t.OppositeSymbol))
            .OrderBy(t => t.DateTime)
            .ToListAsync();

        var count = 0;
        foreach (var trade in tradesWithoutLots)
        {
            try
            {
                // EUR-Preis ermitteln
                decimal eurPrice;
                if (trade.OppositeSymbol.Equals("EUR", StringComparison.OrdinalIgnoreCase))
                {
                    eurPrice = trade.Price;
                }
                else
                {
                    // Für andere Fiat-Währungen: Kurs zum Zeitpunkt holen
                    var (rate, _) = await _coinRateService.GetPreviousCloseRateWithSourceAsync(
                        trade.OppositeSymbol,
                        trade.DateTime.UtcDateTime);
                    eurPrice = rate.HasValue ? trade.Price * rate.Value : trade.Price;
                }

                await CreateLotFromFiatPurchaseAsync(trade, eurPrice);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Erstellen von Lot für Trade #{TradeId}", trade.Id);
            }
        }

        _logger.LogInformation("{Count} Lots aus bestehenden Trades generiert", count);
        return count;
    }

    private static bool IsFiatSymbol(string symbol)
    {
        return FiatSymbolsForQuery.Contains(symbol.ToUpperInvariant());
    }

    #endregion

    #region Transaktionen ohne Lot-Zuordnung

    /// <summary>
    /// Findet alle Transaktionen die eine Lot-Zuordnung benötigen.
    /// </summary>
    public async Task<IList<CryptoTransaction>> GetTransactionsRequiringLotAssignmentAsync()
    {
        return await _dbContext.CryptoTransactions
            .AsNoTracking()
            .Include(t => t.Wallet)
            .Include(t => t.OppositeWallet)
            .Where(t => t.TransactionType == TransactionType.Receive
                     && t.OppositeTransactionId == null
                     && !t.LotAssignmentConfirmed)
            .OrderBy(t => t.DateTime)
            .ToListAsync();
    }

    /// <summary>
    /// Findet alle Trades die eine Lot-Zuordnung benötigen (Verkäufe ohne zugeordnete Lots).
    /// </summary>
    public async Task<IList<CryptoTrade>> GetTradesRequiringLotAssignmentAsync()
    {
        return await _dbContext.CryptoTrades
            .AsNoTracking()
            .Include(t => t.Wallet)
            .Where(t => t.TradeType == TradeType.Sell
                     && !t.LotAssignmentConfirmed)
            .OrderBy(t => t.DateTime)
            .ToListAsync();
    }

    #endregion
}

#region DTOs und Request-Klassen

/// <summary>
/// Zusammenfassung der Lots pro Symbol.
/// </summary>
public class LotSummary
{
    public string Symbol { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public decimal AltbestandQuantity { get; set; }
    public decimal NeubestandQuantity { get; set; }
    public decimal TotalAcquisitionCostEur { get; set; }
    public int LotCount { get; set; }

    /// <summary>
    /// Durchschnittlicher Anschaffungspreis pro Einheit.
    /// </summary>
    public decimal AverageAcquisitionPrice =>
        TotalQuantity > 0 ? TotalAcquisitionCostEur / TotalQuantity : 0;
}

/// <summary>
/// Request für manuelle Lot-Erstellung.
/// </summary>
public class ManualLotRequest
{
    public string Symbol { get; set; } = string.Empty;
    public int WalletId { get; set; }
    public decimal Quantity { get; set; }
    public DateTimeOffset AcquisitionDate { get; set; }
    public decimal AcquisitionPriceEur { get; set; }
    public LotAcquisitionType AcquisitionType { get; set; }
    public int? SourceTransactionId { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// Zuordnung einer bestimmten Menge eines Lots.
/// </summary>
public class LotAllocation
{
    public int LotId { get; set; }
    public decimal Quantity { get; set; }

    /// <summary>
    /// Optional: Lot-Objekt für Anzeige in UI.
    /// </summary>
    public AssetLot? Lot { get; set; }
}

/// <summary>
/// Ergebnis eines Verkaufs.
/// </summary>
public class SaleResult
{
    public decimal TotalQuantity { get; set; }
    public decimal TotalAcquisitionCost { get; set; }
    public decimal TotalSaleProceeds { get; set; }
    public decimal TotalRealizedGain { get; set; }

    /// <summary>
    /// Steuerfreier Gewinn (Altbestand).
    /// </summary>
    public decimal TaxFreeGain { get; set; }
    public decimal TaxFreeQuantity { get; set; }

    /// <summary>
    /// Steuerpflichtiger Gewinn (Neubestand).
    /// </summary>
    public decimal TaxableGain { get; set; }
    public decimal TaxableQuantity { get; set; }

    /// <summary>
    /// Geschätzte KESt (27,5% auf steuerpflichtigen Gewinn).
    /// </summary>
    public decimal EstimatedKESt { get; set; }
}

#endregion
