using CryptoTracker.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CryptoTracker.Services;

/// <summary>
/// Validiert ob ein Lot einen vollständig nachvollziehbaren Flow hat.
/// Nur Lots mit vollständigem Flow können für steuerliche Zwecke verwendet werden.
/// </summary>
public class LotFlowValidator
{
    private readonly CryptoTrackerDbContext _dbContext;
    private readonly ILogger<LotFlowValidator> _logger;

    private static readonly string[] FiatSymbols = { "EUR", "USD", "CHF", "GBP", "ZEUR", "ZUSD", "eur", "usd", "chf", "gbp", "zeur", "zusd" };

    public LotFlowValidator(
        CryptoTrackerDbContext dbContext,
        ILogger<LotFlowValidator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Validiert den Flow eines Lots und gibt das Ergebnis zurück.
    /// </summary>
    public async Task<LotFlowValidationResult> ValidateLotFlowAsync(int lotId)
    {
        var lot = await _dbContext.AssetLots
            .Include(l => l.CurrentWallet)
            .Include(l => l.ParentLot)
            .Include(l => l.SourceTrade)
            .Include(l => l.SourceTransaction)
            .Include(l => l.TransformedFromLots)
            .FirstOrDefaultAsync(l => l.Id == lotId);

        if (lot == null)
        {
            return new LotFlowValidationResult
            {
                IsComplete = false,
                IncompleteReason = $"Lot #{lotId} nicht gefunden"
            };
        }

        return await ValidateLotFlowInternalAsync(lot, new HashSet<int>());
    }

    /// <summary>
    /// Validiert den Flow eines Lots rekursiv.
    /// </summary>
    private async Task<LotFlowValidationResult> ValidateLotFlowInternalAsync(AssetLot lot, HashSet<int> visitedLotIds)
    {
        // Zirkuläre Referenzen vermeiden
        if (visitedLotIds.Contains(lot.Id))
        {
            return new LotFlowValidationResult
            {
                IsComplete = false,
                IncompleteReason = $"Zirkuläre Referenz bei Lot #{lot.Id} erkannt"
            };
        }
        visitedLotIds.Add(lot.Id);

        var result = new LotFlowValidationResult
        {
            LotId = lot.Id,
            Symbol = lot.Symbol,
            Quantity = lot.RemainingQuantity,
            AcquisitionDate = lot.AcquisitionDate
        };

        // Schritt in die Kette hinzufügen
        result.FlowChain.Add(new LotFlowStep
        {
            LotId = lot.Id,
            Symbol = lot.Symbol,
            Quantity = lot.OriginalQuantity,
            Type = MapAcquisitionTypeToFlowStepType(lot.AcquisitionType),
            TradeId = lot.SourceTradeId,
            TransactionId = lot.SourceTransactionId,
            DateTime = lot.AcquisitionDate
        });

        // Fall 1: Fiat-Kauf - Das ist der Ursprung, immer vollständig
        if (lot.AcquisitionType == LotAcquisitionType.FiatPurchase)
        {
            result.IsComplete = true;
            result.EffectiveQuantity = lot.RemainingQuantity;
            return result;
        }

        // Fall 2: Manuelle Einträge - Als vollständig betrachten (User hat Verantwortung)
        if (lot.AcquisitionType == LotAcquisitionType.Manual)
        {
            result.IsComplete = true;
            result.EffectiveQuantity = lot.RemainingQuantity;
            return result;
        }

        // Fall 3: Mining, Staking, Airdrop, etc. - Als vollständig betrachten (Anschaffung zu Zeitwert)
        if (lot.AcquisitionType == LotAcquisitionType.Mining ||
            lot.AcquisitionType == LotAcquisitionType.Staking ||
            lot.AcquisitionType == LotAcquisitionType.Lending ||
            lot.AcquisitionType == LotAcquisitionType.Airdrop ||
            lot.AcquisitionType == LotAcquisitionType.Hardfork ||
            lot.AcquisitionType == LotAcquisitionType.Gift)
        {
            result.IsComplete = true;
            result.EffectiveQuantity = lot.RemainingQuantity;
            return result;
        }

        // Fall 4: Interner Transfer - Prüfe Parent-Lot
        if (lot.AcquisitionType == LotAcquisitionType.InternalTransfer)
        {
            if (lot.ParentLotId == null)
            {
                result.IsComplete = false;
                result.IncompleteReason = $"Interner Transfer ohne Parent-Lot (Lot #{lot.Id})";
                return result;
            }

            // Prüfe ob die Transfer-Transaktion vollständig gepaart ist
            if (lot.SourceTransactionId.HasValue)
            {
                var transaction = await _dbContext.CryptoTransactions
                    .Include(t => t.OppositeTransaction)
                    .FirstOrDefaultAsync(t => t.Id == lot.SourceTransactionId.Value);

                if (transaction?.OppositeTransactionId == null)
                {
                    result.IsComplete = false;
                    result.IncompleteReason = $"Transfer-Transaktion #{lot.SourceTransactionId} hat keine Gegentransaktion";
                    return result;
                }
            }

            // Rekursiv das Parent-Lot validieren
            var parentLot = await _dbContext.AssetLots
                .Include(l => l.CurrentWallet)
                .Include(l => l.ParentLot)
                .Include(l => l.SourceTrade)
                .Include(l => l.SourceTransaction)
                .FirstOrDefaultAsync(l => l.Id == lot.ParentLotId.Value);

            if (parentLot == null)
            {
                result.IsComplete = false;
                result.IncompleteReason = $"Parent-Lot #{lot.ParentLotId} nicht gefunden";
                return result;
            }

            var parentResult = await ValidateLotFlowInternalAsync(parentLot, visitedLotIds);
            result.FlowChain.AddRange(parentResult.FlowChain);
            result.IsComplete = parentResult.IsComplete;
            result.IncompleteReason = parentResult.IncompleteReason;
            result.EffectiveQuantity = parentResult.IsComplete ? lot.RemainingQuantity : 0;
            return result;
        }

        // Fall 5: Crypto-Swap - Prüfe ob Trade gepaart ist und Parent-Lot validieren
        if (lot.AcquisitionType == LotAcquisitionType.CryptoSwap)
        {
            // Prüfe ob der Source-Trade existiert und einen OppositeTrade hat
            if (!lot.SourceTradeId.HasValue)
            {
                result.IsComplete = false;
                result.IncompleteReason = $"Crypto-Swap Lot #{lot.Id} hat keinen Source-Trade";
                return result;
            }

            var trade = await _dbContext.CryptoTrades
                .Include(t => t.OppositeTrade)
                .FirstOrDefaultAsync(t => t.Id == lot.SourceTradeId.Value);

            if (trade == null)
            {
                result.IsComplete = false;
                result.IncompleteReason = $"Source-Trade #{lot.SourceTradeId} nicht gefunden";
                return result;
            }

            if (trade.OppositeTradeId == null)
            {
                result.IsComplete = false;
                result.IncompleteReason = $"Swap-Trade #{trade.Id} hat keinen Gegentrade (OppositeTrade fehlt)";
                return result;
            }

            // Prüfe ob Lots aus der Swap-Quelle kommen (TransformedFromLots)
            if (lot.TransformedFromLots == null || !lot.TransformedFromLots.Any())
            {
                // Alternativ: Prüfe ParentLot
                if (lot.ParentLotId == null)
                {
                    result.IsComplete = false;
                    result.IncompleteReason = $"Crypto-Swap Lot #{lot.Id} hat keine Source-Lots (TransformedFromLots und ParentLot fehlen)";
                    return result;
                }
            }

            // Validiere die Source-Lots
            var sourceLots = lot.TransformedFromLots?.ToList() ?? new List<AssetLot>();
            if (lot.ParentLotId.HasValue && !sourceLots.Any(l => l.Id == lot.ParentLotId.Value))
            {
                var parentLot = await _dbContext.AssetLots
                    .FirstOrDefaultAsync(l => l.Id == lot.ParentLotId.Value);
                if (parentLot != null)
                {
                    sourceLots.Add(parentLot);
                }
            }

            foreach (var sourceLot in sourceLots)
            {
                var sourceResult = await ValidateLotFlowInternalAsync(sourceLot, visitedLotIds);
                result.FlowChain.AddRange(sourceResult.FlowChain);
                
                if (!sourceResult.IsComplete)
                {
                    result.IsComplete = false;
                    result.IncompleteReason = sourceResult.IncompleteReason;
                    return result;
                }
            }

            result.IsComplete = true;
            result.EffectiveQuantity = lot.RemainingQuantity;
            return result;
        }

        // Fall 6: Externe Einzahlung - Ohne weitere Dokumentation unvollständig
        if (lot.AcquisitionType == LotAcquisitionType.ExternalDeposit)
        {
            // Prüfe ob die Transaktion eine Gegentransaktion hat (dann ist es eigentlich ein interner Transfer)
            if (lot.SourceTransactionId.HasValue)
            {
                var transaction = await _dbContext.CryptoTransactions
                    .Include(t => t.OppositeTransaction)
                    .FirstOrDefaultAsync(t => t.Id == lot.SourceTransactionId.Value);

                if (transaction?.OppositeTransactionId != null)
                {
                    // Hat Gegentransaktion - sollte als InternalTransfer klassifiziert werden
                    result.IsComplete = false;
                    result.IncompleteReason = $"Externe Einzahlung #{lot.Id} hat Gegentransaktion - sollte als InternalTransfer klassifiziert werden";
                    return result;
                }
            }

            // Externe Einzahlung ohne Nachweis
            result.IsComplete = false;
            result.IncompleteReason = $"Externe Einzahlung #{lot.Id} ohne vollständige Herkunftsdokumentation";
            return result;
        }

        // Unbekannter Fall
        result.IsComplete = false;
        result.IncompleteReason = $"Unbekannter Akquisitionstyp: {lot.AcquisitionType}";
        return result;
    }

    /// <summary>
    /// Validiert alle Lots und aktualisiert deren IsFlowComplete-Status.
    /// </summary>
    public async Task<int> ValidateAndUpdateAllLotsAsync()
    {
        var lots = await _dbContext.AssetLots
            .Where(l => l.RemainingQuantity > 0)
            .ToListAsync();

        var updatedCount = 0;

        foreach (var lot in lots)
        {
            try
            {
                var result = await ValidateLotFlowAsync(lot.Id);
                
                if (lot.IsFlowComplete != result.IsComplete || lot.FlowIncompleteReason != result.IncompleteReason)
                {
                    lot.IsFlowComplete = result.IsComplete;
                    lot.FlowIncompleteReason = result.IncompleteReason;
                    updatedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei der Validierung von Lot #{LotId}", lot.Id);
                lot.IsFlowComplete = false;
                lot.FlowIncompleteReason = $"Validierungsfehler: {ex.Message}";
                updatedCount++;
            }
        }

        if (updatedCount > 0)
        {
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{Count} Lots wurden aktualisiert", updatedCount);
        }

        return updatedCount;
    }

    /// <summary>
    /// Holt alle Lots die einen unvollständigen Flow haben.
    /// </summary>
    public async Task<IList<AssetLot>> GetLotsWithIncompleteFlowAsync(int? walletId = null)
    {
        var query = _dbContext.AssetLots
            .AsNoTracking()
            .Include(l => l.CurrentWallet)
            .Where(l => l.RemainingQuantity > 0 && !l.IsFlowComplete);

        if (walletId.HasValue)
        {
            query = query.Where(l => l.CurrentWalletId == walletId.Value);
        }

        return await query.OrderBy(l => l.AcquisitionDate).ToListAsync();
    }

    private static LotFlowStepType MapAcquisitionTypeToFlowStepType(LotAcquisitionType acquisitionType)
    {
        return acquisitionType switch
        {
            LotAcquisitionType.FiatPurchase => LotFlowStepType.FiatPurchase,
            LotAcquisitionType.CryptoSwap => LotFlowStepType.Swap,
            LotAcquisitionType.InternalTransfer => LotFlowStepType.Transfer,
            LotAcquisitionType.ExternalDeposit => LotFlowStepType.ExternalDeposit,
            LotAcquisitionType.Mining => LotFlowStepType.Mining,
            LotAcquisitionType.Staking => LotFlowStepType.Staking,
            LotAcquisitionType.Lending => LotFlowStepType.Lending,
            LotAcquisitionType.Airdrop => LotFlowStepType.Airdrop,
            LotAcquisitionType.Hardfork => LotFlowStepType.Hardfork,
            LotAcquisitionType.Gift => LotFlowStepType.Gift,
            LotAcquisitionType.Manual => LotFlowStepType.Manual,
            _ => LotFlowStepType.Unknown
        };
    }
}

#region Result-Klassen

/// <summary>
/// Ergebnis der Flow-Validierung für ein Lot.
/// </summary>
public class LotFlowValidationResult
{
    public int LotId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public DateTimeOffset AcquisitionDate { get; set; }
    
    /// <summary>
    /// Ist der Flow vollständig nachvollziehbar?
    /// </summary>
    public bool IsComplete { get; set; }
    
    /// <summary>
    /// Grund für unvollständigen Flow.
    /// </summary>
    public string? IncompleteReason { get; set; }
    
    /// <summary>
    /// Die Kette der Schritte bis zum Ursprung.
    /// </summary>
    public List<LotFlowStep> FlowChain { get; set; } = new();
    
    /// <summary>
    /// Tatsächlich verfügbare Menge (nur bei vollständigem Flow > 0).
    /// </summary>
    public decimal EffectiveQuantity { get; set; }
}

/// <summary>
/// Ein Schritt in der Flow-Kette.
/// </summary>
public class LotFlowStep
{
    public int LotId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public LotFlowStepType Type { get; set; }
    public int? TradeId { get; set; }
    public int? TransactionId { get; set; }
    public DateTimeOffset DateTime { get; set; }
}

/// <summary>
/// Art eines Flow-Schritts.
/// </summary>
public enum LotFlowStepType
{
    Unknown,
    FiatPurchase,
    Swap,
    Transfer,
    ExternalDeposit,
    Mining,
    Staking,
    Lending,
    Airdrop,
    Hardfork,
    Gift,
    Manual
}

#endregion
