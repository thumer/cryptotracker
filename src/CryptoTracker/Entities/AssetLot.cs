namespace CryptoTracker.Entities;

/// <summary>
/// Repräsentiert eine spezifische Tranche eines Assets mit eindeutiger Herkunft.
/// Jedes Lot hat ein Kaufdatum, Kaufpreis und kann als Altbestand (vor 28.02.2021) markiert sein.
/// </summary>
public class AssetLot
{
    public int Id { get; set; }

    /// <summary>
    /// Welches Asset (z.B. BTC, ETH)
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Wo liegt das Lot aktuell?
    /// </summary>
    public int CurrentWalletId { get; set; }
    public Wallet CurrentWallet { get; set; } = null!;

    /// <summary>
    /// Aktuelle Menge (kann durch Teil-Verkäufe/Transfers abnehmen)
    /// </summary>
    public decimal RemainingQuantity { get; set; }

    /// <summary>
    /// Ursprüngliche Menge bei Erstellung des Lots
    /// </summary>
    public decimal OriginalQuantity { get; set; }

    // === Herkunftsinformationen ===

    /// <summary>
    /// Wann wurde dieses Lot ursprünglich erworben?
    /// Entscheidend für Altbestand/Neubestand-Klassifizierung
    /// </summary>
    public DateTimeOffset AcquisitionDate { get; set; }

    /// <summary>
    /// Anschaffungskosten pro Einheit in EUR
    /// </summary>
    public decimal AcquisitionPriceEur { get; set; }

    /// <summary>
    /// Gesamte Anschaffungskosten (inkl. Gebühren) in EUR
    /// </summary>
    public decimal TotalAcquisitionCostEur { get; set; }

    /// <summary>
    /// Wie wurde das Lot erworben?
    /// </summary>
    public LotAcquisitionType AcquisitionType { get; set; }

    /// <summary>
    /// Referenz zur ursprünglichen Transaktion (bei Receive/Transfer)
    /// </summary>
    public int? SourceTransactionId { get; set; }
    public CryptoTransaction? SourceTransaction { get; set; }

    /// <summary>
    /// Referenz zum ursprünglichen Trade (bei Kauf)
    /// </summary>
    public int? SourceTradeId { get; set; }
    public CryptoTrade? SourceTrade { get; set; }

    /// <summary>
    /// Bei Transfer oder Krypto-zu-Krypto-Tausch: Lot des Quell-Assets.
    /// Ermöglicht Rückverfolgung der Anschaffungskosten.
    /// </summary>
    public int? ParentLotId { get; set; }
    public AssetLot? ParentLot { get; set; }

    /// <summary>
    /// Child-Lots die aus diesem Lot entstanden sind (z.B. bei Teil-Transfer)
    /// </summary>
    public ICollection<AssetLot> ChildLots { get; set; } = new List<AssetLot>();

    /// <summary>
    /// Bewegungen (Verwendungen) dieses Lots
    /// </summary>
    public ICollection<LotMovement> Movements { get; set; } = new List<LotMovement>();

    // === Flow-Tracking ===

    /// <summary>
    /// Ist der gesamte Flow dieses Lots vollständig nachvollziehbar?
    /// Nur wenn true, kann das Lot für steuerliche Zwecke verwendet werden.
    /// </summary>
    public bool IsFlowComplete { get; set; } = false;

    /// <summary>
    /// Grund warum der Flow unvollständig ist (falls IsFlowComplete = false)
    /// </summary>
    public string? FlowIncompleteReason { get; set; }

    /// <summary>
    /// Bei Crypto-zu-Crypto Swap: Das Lot wurde in ein anderes Asset transformiert.
    /// Referenz auf das neue Lot (z.B. BTC -> ETH Swap: BTC-Lot verweist auf ETH-Lot)
    /// </summary>
    public int? TransformedToLotId { get; set; }
    public AssetLot? TransformedToLot { get; set; }

    /// <summary>
    /// Lots die durch Transformation in dieses Lot eingegangen sind
    /// </summary>
    public ICollection<AssetLot> TransformedFromLots { get; set; } = new List<AssetLot>();

    /// <summary>
    /// Benutzernotiz zur Herkunft
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Zeitpunkt der Erstellung dieses Lot-Eintrags
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // === Berechnete Properties ===

    /// <summary>
    /// Ist Altbestand (erworben bis einschließlich 28.02.2021)?
    /// Nach der ökosozialen Steuerreform: Altbestand mit >1 Jahr Haltefrist ist steuerfrei.
    /// </summary>
    public bool IsAltbestand => AcquisitionDate <= AltbestandStichtag;

    /// <summary>
    /// Stichtag für Altbestand-Klassifizierung (28.02.2021 23:59:59 UTC)
    /// </summary>
    public static readonly DateTimeOffset AltbestandStichtag = new(2021, 2, 28, 23, 59, 59, TimeSpan.Zero);

    /// <summary>
    /// Ist dieses Lot vollständig aufgebraucht?
    /// </summary>
    public bool IsFullyConsumed => RemainingQuantity <= 0;

    /// <summary>
    /// Anschaffungskosten für die verbleibende Menge
    /// </summary>
    public decimal RemainingAcquisitionCostEur => RemainingQuantity * AcquisitionPriceEur;
}

/// <summary>
/// Art der Akquisition eines Lots
/// </summary>
public enum LotAcquisitionType
{
    /// <summary>Kauf mit Fiat (EUR, USD etc.)</summary>
    FiatPurchase,

    /// <summary>Erhalt durch Krypto-zu-Krypto-Tausch</summary>
    CryptoSwap,

    /// <summary>Transfer von eigener Börse/Wallet (Herkunft bekannt, verknüpft)</summary>
    InternalTransfer,

    /// <summary>Transfer von externer Quelle (Herkunft muss dokumentiert werden)</summary>
    ExternalDeposit,

    /// <summary>Mining-Rewards</summary>
    Mining,

    /// <summary>Staking-Rewards</summary>
    Staking,

    /// <summary>Lending-Zinsen</summary>
    Lending,

    /// <summary>Airdrop</summary>
    Airdrop,

    /// <summary>Hardfork</summary>
    Hardfork,

    /// <summary>Schenkung erhalten</summary>
    Gift,

    /// <summary>Manueller Eintrag (z.B. für Altbestand-Import)</summary>
    Manual
}
