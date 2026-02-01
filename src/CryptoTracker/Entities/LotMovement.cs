namespace CryptoTracker.Entities;

/// <summary>
/// Dokumentiert jede Verwendung eines Lots (Transfer, Verkauf, Swap, etc.)
/// </summary>
public class LotMovement
{
    public int Id { get; set; }

    /// <summary>
    /// Welches Lot wurde verwendet?
    /// </summary>
    public int LotId { get; set; }
    public AssetLot Lot { get; set; } = null!;

    /// <summary>
    /// Wieviel wurde von diesem Lot verwendet?
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Zeitpunkt der Bewegung
    /// </summary>
    public DateTimeOffset DateTime { get; set; }

    /// <summary>
    /// Art der Bewegung
    /// </summary>
    public LotMovementType MovementType { get; set; }

    // === Referenzen zur auslösenden Aktion ===

    /// <summary>
    /// Referenz zum auslösenden Trade (bei Verkauf/Swap)
    /// </summary>
    public int? TradeId { get; set; }
    public CryptoTrade? Trade { get; set; }

    /// <summary>
    /// Referenz zur auslösenden Transaktion (bei Transfer)
    /// </summary>
    public int? TransactionId { get; set; }
    public CryptoTransaction? Transaction { get; set; }

    // === Bei Verkauf: Steuer-relevante Daten ===

    /// <summary>
    /// Bei Verkauf: Erlös pro Einheit in EUR
    /// </summary>
    public decimal? SalePriceEur { get; set; }

    /// <summary>
    /// Bei Verkauf: Realisierter Gewinn/Verlust in EUR
    /// Berechnung: (SalePriceEur - AcquisitionPriceEur) * Quantity
    /// </summary>
    public decimal? RealizedGainEur { get; set; }

    /// <summary>
    /// War diese Bewegung steuerfrei? (Altbestand oder Krypto-zu-Krypto)
    /// </summary>
    public bool IsTaxFree { get; set; }

    /// <summary>
    /// Grund für Steuerfreiheit (falls IsTaxFree = true)
    /// </summary>
    public TaxFreeReason? TaxFreeReason { get; set; }

    // === Bei Transfer: Referenz zum resultierenden Lot ===

    /// <summary>
    /// Bei Transfer: Neues Lot auf Ziel-Wallet
    /// </summary>
    public int? ResultingLotId { get; set; }
    public AssetLot? ResultingLot { get; set; }

    /// <summary>
    /// Zeitpunkt der Erstellung dieses Movement-Eintrags
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Benutzernotiz
    /// </summary>
    public string? Note { get; set; }
}

/// <summary>
/// Art der Lot-Bewegung
/// </summary>
public enum LotMovementType
{
    /// <summary>Transfer zu anderem Wallet (gleiches Asset)</summary>
    Transfer,

    /// <summary>Verkauf gegen Fiat</summary>
    FiatSale,

    /// <summary>Verwendung in Krypto-zu-Krypto-Tausch (als Ausgabe)</summary>
    CryptoSwapOut,

    /// <summary>Gebühr bezahlt</summary>
    Fee,

    /// <summary>Schenkung gegeben</summary>
    GiftOut,

    /// <summary>Verlust (z.B. durch Hack, verlorene Keys)</summary>
    Loss
}

/// <summary>
/// Grund für Steuerfreiheit einer Bewegung
/// </summary>
public enum TaxFreeReason
{
    /// <summary>Altbestand (vor 28.02.2021 erworben, >1 Jahr gehalten)</summary>
    Altbestand,

    /// <summary>Krypto-zu-Krypto-Tausch (steuerneutral, Anschaffungskosten werden weitergegeben)</summary>
    CryptoSwap,

    /// <summary>Transfer zwischen eigenen Wallets</summary>
    InternalTransfer
}
