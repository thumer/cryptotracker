# Plan: Krypto-Steuer Österreich – Feingranulares Asset-Tracking & Finanzamt-Reporting

## Zusammenfassung

Dieses Dokument beschreibt die Erweiterung des CryptoTracker-Systems für die österreichische Krypto-Steuer. 
Kern ist ein **feingranulares Asset-Tracking-System**, das:
1. Jeden Krypto-Betrag mit seiner Herkunft (Kaufdatum, Quelle, Preis) verknüpft
2. Bei Transfers **keine automatische Zuordnung (FIFO)** vornimmt – der Benutzer entscheidet explizit
3. **Altbestand** (vor 28.02.2021) und **Neubestand** getrennt verwaltet
4. Finanzamt-konforme Reports generiert

---

## 1. Rechtliche Grundlagen Österreich

### 1.1 Steuerpflicht nach Ökosozialer Steuerreform (seit 01.03.2022)

| Vorgang | Steuerpflicht | Steuersatz |
|---------|---------------|------------|
| Kauf Fiat → Krypto | ❌ Steuerfrei | - |
| Tausch Krypto → Krypto | ❌ Steuerfrei | - (Anschaffungskosten werden weitergegeben) |
| Transfer zwischen eigenen Wallets | ❌ Steuerfrei | - |
| Verkauf Krypto → Fiat (Neubestand) | ✅ Steuerpflichtig | 27,5% KESt |
| Verkauf Altbestand (vor 28.02.2021) | ❌ Steuerfrei | - (Haltefrist > 1 Jahr erfüllt) |
| Mining, Lending | ✅ Bei Zufluss + Verkauf | 27,5% |
| Staking, Airdrops, Hardforks | ❌ Bei Zufluss steuerfrei | 27,5% bei Verkauf |

### 1.2 Altbestand vs. Neubestand

- **Altbestand**: Erworben **bis einschließlich 28. Februar 2021**
  - Nach einem Jahr Haltefrist (also ab 01.03.2022) komplett steuerfrei
  - Kann frei verkauft werden ohne KESt
  
- **Neubestand**: Erworben **ab 1. März 2021**
  - Kein Haltefrist-Vorteil mehr
  - Verkauf gegen Fiat: immer 27,5% KESt auf Gewinn

### 1.3 Einkünfteermittlung: Gleitender Durchschnittspreis (ACB)

Ab 01.01.2023 gilt gemäß BMF-Kryptowährungsverordnung:
- Assets auf **derselben Kryptowährungsadresse** werden mit dem **gleitenden Durchschnittspreis (ACB = Average Cost Basis)** bewertet
- **NICHT in den ACB eingehen**:
  - Altbestand (separate Tranchenverwaltung)
  - Pauschal angesetzte Anschaffungskosten beim KESt-Abzug
- Für Veräußerungen **vor 31.12.2022** galt FIFO oder freie Zuordnung

### 1.4 Was das Finanzamt braucht

1. **Gewinn/Verlust-Aufstellung** pro Steuerjahr
2. **Nachweis Altbestand**: Kaufdatum, Kaufpreis, Wallet-Adressen
3. **Einzeltransaktionsaufstellung** bei Prüfung
4. **Mittelherkunftsnachweis** bei Auszahlungen auf Bankkonten

---

## 2. Dein Anforderungsprofil

### 2.1 Feingranulare Kontrolle statt FIFO

> "Ich möchte nie eine Default-Annahme, wenn es eine Alt- oder Neubestand-Verschiebung gibt"

**Lösung**: Jede Coin-Menge wird als **"Lot"** (Tranche) mit Herkunft gespeichert. Bei Transfers/Verkäufen wählt der User explizit aus, welche Lots verwendet werden.

### 2.2 Verbindung von Transaktionen

> "Obwohl die meisten Transaktionen (in and out) miteinander verbunden sind, gibt es welche die nicht verbunden sind"

**Lösung**: Unverknüpfte Transaktionen werden markiert und erfordern manuelle Zuordnung der Lot-Herkunft.

### 2.3 Geldfluss-Visualisierung

> "Wie visualisiere ich den Geldfluss – primär fürs Finanzamt, aber auch für mich"

**Lösung**: Sankey-Diagramme und Fluss-Tabellen, die zeigen:
- Woher kam das Asset (Kauf, Staking, Transfer von Wallet X)
- Wohin ging es (Transfer, Verkauf)
- Steuerliche Klassifikation (Altbestand/Neubestand)

---

## 3. Datenmodell-Erweiterungen

### 3.1 Neue Entity: `AssetLot` (Tranche)

Jedes "Lot" repräsentiert eine spezifische Menge eines Assets mit eindeutiger Herkunft.

```csharp
public class AssetLot
{
    public int Id { get; set; }
    
    // Welches Asset (z.B. BTC, ETH)
    public string Symbol { get; set; } = string.Empty;
    
    // Wo liegt das Lot aktuell?
    public int CurrentWalletId { get; set; }
    public Wallet CurrentWallet { get; set; } = null!;
    
    // Aktuelle Menge (kann durch Teil-Verkäufe abnehmen)
    public decimal Quantity { get; set; }
    
    // Ursprüngliche Menge bei Erstellung des Lots
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
    /// Gesamte Anschaffungskosten (inkl. Gebühren)
    /// </summary>
    public decimal TotalAcquisitionCostEur { get; set; }
    
    /// <summary>
    /// Wie wurde das Lot erworben?
    /// </summary>
    public LotAcquisitionType AcquisitionType { get; set; }
    
    /// <summary>
    /// Referenz zur ursprünglichen Transaktion/Trade
    /// </summary>
    public int? SourceTradeId { get; set; }
    public CryptoTrade? SourceTrade { get; set; }
    
    public int? SourceTransactionId { get; set; }
    public CryptoTransaction? SourceTransaction { get; set; }
    
    /// <summary>
    /// Bei Krypto-zu-Krypto-Tausch: Lot des eingetauschten Assets
    /// Ermöglicht Rückverfolgung der Anschaffungskosten
    /// </summary>
    public int? ParentLotId { get; set; }
    public AssetLot? ParentLot { get; set; }
    
    /// <summary>
    /// Ist Altbestand (vor 28.02.2021)?
    /// </summary>
    public bool IsAltbestand => AcquisitionDate <= new DateTimeOffset(2021, 2, 28, 23, 59, 59, TimeSpan.Zero);
    
    /// <summary>
    /// Benutzernotiz zur Herkunft
    /// </summary>
    public string? Note { get; set; }
    
    /// <summary>
    /// Ist dieses Lot vollständig aufgebraucht?
    /// </summary>
    public bool IsFullyConsumed => Quantity <= 0;
}

public enum LotAcquisitionType
{
    /// <summary>Kauf mit Fiat (EUR, USD etc.)</summary>
    FiatPurchase,
    
    /// <summary>Erhalt durch Krypto-zu-Krypto-Tausch</summary>
    CryptoSwap,
    
    /// <summary>Transfer von anderer Börse/Wallet (Herkunft bekannt)</summary>
    InternalTransfer,
    
    /// <summary>Transfer von externer Quelle (Herkunft muss dokumentiert werden)</summary>
    ExternalTransfer,
    
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
```

### 3.2 Neue Entity: `LotMovement` (Lot-Bewegung)

Dokumentiert jede Verwendung eines Lots.

```csharp
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
    
    /// <summary>
    /// Bei Verkauf: Erlös pro Einheit in EUR
    /// </summary>
    public decimal? SalePriceEur { get; set; }
    
    /// <summary>
    /// Bei Verkauf: Realisierter Gewinn/Verlust
    /// </summary>
    public decimal? RealizedGainEur { get; set; }
    
    /// <summary>
    /// War diese Bewegung steuerfrei (Altbestand)?
    /// </summary>
    public bool IsTaxFree { get; set; }
    
    /// <summary>
    /// Referenz zur auslösenden Transaktion/Trade
    /// </summary>
    public int? TradeId { get; set; }
    public CryptoTrade? Trade { get; set; }
    
    public int? TransactionId { get; set; }
    public CryptoTransaction? Transaction { get; set; }
    
    /// <summary>
    /// Bei Transfer: Neues Lot auf Ziel-Wallet
    /// </summary>
    public int? ResultingLotId { get; set; }
    public AssetLot? ResultingLot { get; set; }
}

public enum LotMovementType
{
    /// <summary>Transfer zu anderem Wallet (gleiches Asset)</summary>
    Transfer,
    
    /// <summary>Verkauf gegen Fiat</summary>
    FiatSale,
    
    /// <summary>Verwendung in Krypto-zu-Krypto-Tausch</summary>
    CryptoSwap,
    
    /// <summary>Gebühr bezahlt</summary>
    Fee,
    
    /// <summary>Schenkung gegeben</summary>
    GiftOut
}
```

### 3.3 Erweiterung bestehender Entities

#### CryptoTransaction

```csharp
public class CryptoTransaction : IFlow
{
    // ... bestehende Properties ...
    
    /// <summary>
    /// Bei Send: Welche Lots wurden für diese Transaktion verwendet?
    /// Bei Receive ohne verknüpfte Gegentransaktion: Manuelle Lot-Zuweisung erforderlich
    /// </summary>
    public ICollection<LotMovement> LotMovements { get; set; } = new List<LotMovement>();
    
    /// <summary>
    /// Wurde die Lot-Zuordnung für diese Transaktion bestätigt?
    /// </summary>
    public bool LotAssignmentConfirmed { get; set; }
    
    /// <summary>
    /// Benötigt manuelle Lot-Zuordnung?
    /// </summary>
    public bool RequiresLotAssignment => 
        TransactionType == TransactionType.Receive && 
        OppositeTransactionId == null && 
        !LotAssignmentConfirmed;
}
```

#### CryptoTrade

```csharp
public class CryptoTrade : IFlow
{
    // ... bestehende Properties ...
    
    /// <summary>
    /// Bei Sell: Welche Lots wurden verkauft?
    /// Bei Buy mit Krypto-Zahlung: Welche Lots wurden verwendet?
    /// </summary>
    public ICollection<LotMovement> LotMovements { get; set; } = new List<LotMovement>();
    
    /// <summary>
    /// Wurde die Lot-Zuordnung für diesen Trade bestätigt?
    /// </summary>
    public bool LotAssignmentConfirmed { get; set; }
    
    /// <summary>
    /// Bei Buy: Erstelltes Lot
    /// </summary>
    public int? ResultingLotId { get; set; }
    public AssetLot? ResultingLot { get; set; }
}
```

---

## 4. Workflow: Feingranulare Lot-Zuordnung

### 4.1 Szenario: Kauf mit Fiat

```
User kauft 1 BTC für 50.000€ auf Bitpanda am 15.03.2024
```

**Automatische Aktion:**
1. Trade wird erfasst (TradeType: Buy)
2. Neues `AssetLot` wird erstellt:
   - Symbol: BTC
   - CurrentWallet: Bitpanda
   - Quantity: 1.0
   - AcquisitionDate: 15.03.2024
   - AcquisitionPriceEur: 50.000€
   - AcquisitionType: FiatPurchase
   - IsAltbestand: false (Neubestand)

### 4.2 Szenario: Transfer zwischen Börsen

```
User transferiert 0.5 BTC von Bitpanda zu Binance
```

**Workflow mit manueller Auswahl:**

1. **Send-Transaktion** wird auf Bitpanda erfasst
2. **Receive-Transaktion** wird auf Binance erfasst
3. Transaktionen werden verknüpft (OppositeTransactionId)

4. **UI zeigt Dialog zur Lot-Auswahl:**
   ```
   ┌────────────────────────────────────────────────────────┐
   │ Transfer 0.5 BTC: Bitpanda → Binance                  │
   │                                                        │
   │ Welche Lots sollen verwendet werden?                   │
   │                                                        │
   │ ┌─────────────────────────────────────────────────────┐│
   │ │ ☑ Lot #1: 0.3 BTC                                  ││
   │ │   Erworben: 15.01.2020 (ALTBESTAND ✓)              ││
   │ │   Kaufpreis: 8.500€                                ││
   │ │   Verwenden: [0.3    ] BTC                         ││
   │ │─────────────────────────────────────────────────────││
   │ │ ☑ Lot #2: 1.0 BTC                                  ││
   │ │   Erworben: 15.03.2024 (Neubestand)                ││
   │ │   Kaufpreis: 50.000€                               ││
   │ │   Verwenden: [0.2    ] BTC                         ││
   │ │─────────────────────────────────────────────────────││
   │ │ ☐ Lot #3: 0.5 BTC                                  ││
   │ │   Erworben: 01.06.2024 (Neubestand)                ││
   │ │   Kaufpreis: 65.000€                               ││
   │ └─────────────────────────────────────────────────────┘│
   │                                                        │
   │ Ausgewählt: 0.5 BTC (0.3 Altbestand + 0.2 Neubestand) │
   │                                                        │
   │ [Abbrechen]                        [Zuordnung speichern]│
   └────────────────────────────────────────────────────────┘
   ```

5. **Nach Bestätigung:**
   - Lot #1: Quantity = 0.3 - 0.3 = 0 (vollständig verbraucht)
   - Lot #2: Quantity = 1.0 - 0.2 = 0.8 (teilweise verbraucht)
   - Zwei neue Lots auf Binance:
     - Lot #4: 0.3 BTC, Altbestand, ParentLot = #1
     - Lot #5: 0.2 BTC, Neubestand, ParentLot = #2

### 4.3 Szenario: Verkauf gegen Fiat

```
User verkauft 0.5 BTC für 35.000€
```

**Workflow:**

1. **UI zeigt verfügbare Lots:**
   ```
   ┌────────────────────────────────────────────────────────┐
   │ Verkauf 0.5 BTC für 35.000€                           │
   │                                                        │
   │ Welche Lots sollen verkauft werden?                    │
   │                                                        │
   │ ┌─────────────────────────────────────────────────────┐│
   │ │ ☑ Lot #4: 0.3 BTC (ALTBESTAND)                     ││
   │ │   Kaufpreis: 8.500€ → Verkauf: 21.000€             ││
   │ │   Gewinn: 12.500€ → STEUERFREI ✓                   ││
   │ │─────────────────────────────────────────────────────││
   │ │ ☑ Lot #5: 0.2 BTC (Neubestand)                     ││
   │ │   Kaufpreis: 10.000€ → Verkauf: 14.000€            ││
   │ │   Gewinn: 4.000€ → KESt: 1.100€                    ││
   │ └─────────────────────────────────────────────────────┘│
   │                                                        │
   │ Zusammenfassung:                                       │
   │ • Steuerfreier Gewinn (Altbestand): 12.500€           │
   │ • Steuerpflichtiger Gewinn:          4.000€           │
   │ • Zu zahlende KESt (27,5%):          1.100€           │
   │                                                        │
   │ [Abbrechen]                        [Verkauf bestätigen]│
   └────────────────────────────────────────────────────────┘
   ```

### 4.4 Szenario: Unverknüpfte Receive-Transaktion

```
User erhält 0.5 BTC auf Binance, aber keine zugehörige Send-Transaktion ist erfasst
```

**Workflow:**

1. System erkennt: Receive ohne OppositeTransaction
2. **Transaktion wird als "Lot-Zuordnung erforderlich" markiert**
3. UI zeigt Warnung in Transaktionsliste

4. **User muss Herkunft dokumentieren:**
   ```
   ┌────────────────────────────────────────────────────────┐
   │ ⚠ Unverknüpfte Einzahlung: 0.5 BTC                    │
   │                                                        │
   │ Bitte dokumentieren Sie die Herkunft:                  │
   │                                                        │
   │ Herkunftstyp:                                          │
   │ ○ Transfer von eigener Wallet (nicht in System)        │
   │ ○ Kauf auf anderer Börse (mit Fiat)                   │
   │ ○ Mining-Reward                                        │
   │ ○ Staking-Reward                                       │
   │ ○ Airdrop                                              │
   │ ○ Schenkung                                            │
   │ ● Altbestand-Import                                    │
   │                                                        │
   │ Ursprüngliches Kaufdatum: [15.01.2020    ]            │
   │ Ursprünglicher Kaufpreis: [8.500         ] EUR        │
   │ Notiz: [Kauf auf Kraken, Wallet geschlossen____]      │
   │                                                        │
   │ [Abbrechen]                        [Lot erstellen]     │
   └────────────────────────────────────────────────────────┘
   ```

---

## 5. Finanzamt-Reporting

### 5.1 Steuerbericht-Struktur

```
┌────────────────────────────────────────────────────────────┐
│           KRYPTO-STEUERBERICHT 2025                        │
│           Max Mustermann                                   │
│           Erstellt: 15.04.2026                             │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 1. ZUSAMMENFASSUNG                                         │
│ ─────────────────────────────────────────────────────────  │
│                                                            │
│ Einkünfte aus Kapitalvermögen (§ 27b EStG):               │
│                                                            │
│   Realisierte Gewinne (Neubestand):        15.420,00 €    │
│   Realisierte Verluste (Neubestand):       -2.340,00 €    │
│   ──────────────────────────────────────────────────────   │
│   Netto Einkünfte:                         13.080,00 €    │
│   KESt (27,5%):                             3.597,00 €    │
│                                                            │
│   Steuerfreie Gewinne (Altbestand):        42.500,00 €    │
│                                                            │
│ Laufende Einkünfte (Mining, Lending):                      │
│   Mining:                                   1.200,00 €    │
│   Lending:                                    450,00 €    │
│   ──────────────────────────────────────────────────────   │
│   Summe (27,5% bei Zufluss):                1.650,00 €    │
│   Bereits versteuert bei Zufluss:             453,75 €    │
│                                                            │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 2. ALTBESTAND-NACHWEIS                                     │
│ ─────────────────────────────────────────────────────────  │
│                                                            │
│ Die folgenden Assets wurden vor dem 28.02.2021 erworben   │
│ und sind daher steuerfrei:                                │
│                                                            │
│ ┌──────────┬──────────┬────────────┬─────────────────────┐│
│ │ Asset    │ Menge    │ Kaufdatum  │ Kaufpreis (€)       ││
│ ├──────────┼──────────┼────────────┼─────────────────────┤│
│ │ BTC      │ 2.5      │ 15.01.2020 │ 21.250,00           ││
│ │ ETH      │ 10.0     │ 22.06.2020 │ 2.150,00            ││
│ │ LTC      │ 50.0     │ 03.12.2019 │ 2.300,00            ││
│ └──────────┴──────────┴────────────┴─────────────────────┘│
│                                                            │
│ Gesamt Altbestand Anschaffungskosten:      25.700,00 €    │
│ Verkauft in 2025:                                          │
│   BTC 1.0 für 68.200€ → Gewinn 42.500€ (steuerfrei)       │
│                                                            │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 3. EINZELTRANSAKTIONEN (Verkäufe)                          │
│ ─────────────────────────────────────────────────────────  │
│                                                            │
│ ┌────────────┬───────┬────────┬──────────┬────────┬──────┐│
│ │ Datum      │ Asset │ Menge  │ Erlös(€) │ AK(€)  │ G/V  ││
│ ├────────────┼───────┼────────┼──────────┼────────┼──────┤│
│ │ 15.03.2025 │ BTC   │ 0.5    │ 34.100   │ 12.500 │21.600││
│ │ 22.05.2025 │ ETH   │ 5.0    │ 17.500   │ 11.250 │ 6.250││
│ │ ...        │ ...   │ ...    │ ...      │ ...    │ ...  ││
│ └────────────┴───────┴────────┴──────────┴────────┴──────┘│
│                                                            │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 4. ASSET-FLUSS-DOKUMENTATION                               │
│ ─────────────────────────────────────────────────────────  │
│                                                            │
│ BTC-Fluss 2025:                                            │
│                                                            │
│ ┌─────────────┐    ┌─────────────┐    ┌─────────────┐     │
│ │  Bitpanda   │───▶│   Ledger    │───▶│   Verkauf   │     │
│ │  Kauf 1.0   │    │  Transfer   │    │    0.5      │     │
│ │  50.000€    │    │             │    │  34.100€    │     │
│ └─────────────┘    └─────────────┘    └─────────────┘     │
│     ▲                                                      │
│     │ Herkunft: Neubestand (15.03.2024)                   │
│     │ Anschaffungskosten: 25.000€ (für 0.5 BTC)           │
│     │ Gewinn: 9.100€ → KESt: 2.502,50€                    │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

### 5.2 Für FinanzOnline relevante Kennzahlen

| Kennzahl | Beschreibung | Formular-Feld |
|----------|--------------|---------------|
| KZ 981 | Kapitalerträge aus Kryptowährungen | E1 Anlage KAP |
| KZ 982 | Verluste aus Kryptowährungen | E1 Anlage KAP |
| KZ 994 | Bereits abgeführte KESt | E1 Anlage KAP |

---

## 6. Geldfluss-Visualisierung

### 6.1 Sankey-Diagramm

Für die Visualisierung des Asset-Flusses empfehle ich ein Sankey-Diagramm:

```
                    ┌──────────────────┐
   Käufe (Fiat)     │                  │     Verkäufe (Fiat)
   ─────────────────▶  BTC Gesamtpool  ▶─────────────────────
   50.000€          │                  │     68.200€
                    │    2.5 BTC       │
   Transfers In     │                  │     Transfers Out
   ─────────────────▶                  ▶─────────────────────
   (von Kraken)     │                  │     (zu Ledger)
                    └──────────────────┘
                           │
                           ▼
                    Altbestand: 1.5 BTC
                    Neubestand: 1.0 BTC
```

### 6.2 Timeline-Ansicht

```
2020 ──●────────────────────────────────────────────────────▶
       │
       └─ 15.01: Kauf 2.5 BTC @ 8.500€ (Altbestand)
       
2021 ──────────●───────────────────────────────────────────▶
               │
               └─ 28.02: STICHTAG ALTBESTAND
       
2024 ──────────────────●───────────────────────────────────▶
                       │
                       └─ 15.03: Kauf 1.0 BTC @ 50.000€ (Neubestand)
                       
2025 ──────────────────────────●───────●───────────────────▶
                               │       │
                               │       └─ 22.05: Verkauf 0.5 BTC (Neubestand)
                               │              Gewinn: 9.100€ → KESt: 2.502,50€
                               │
                               └─ 15.03: Verkauf 1.0 BTC (Altbestand)
                                      Gewinn: 42.500€ → STEUERFREI
```

---

## 7. Implementierungs-Arbeitspakete

### AP 1: Datenmodell & Migration

**Aufwand:** ~8h

1. Neue Entities erstellen:
   - `AssetLot`
   - `LotMovement`
2. Bestehende Entities erweitern:
   - `CryptoTransaction`
   - `CryptoTrade`
3. EF Core Migration erstellen
4. Indizes für Performance

### AP 2: Lot-Service

**Aufwand:** ~12h

```csharp
public interface ILotService
{
    // Lot-Management
    Task<AssetLot> CreateLotFromPurchase(CryptoTrade trade);
    Task<AssetLot> CreateLotFromTransfer(CryptoTransaction transaction, AssetLot parentLot, decimal quantity);
    Task<AssetLot> CreateManualLot(ManualLotRequest request);
    
    // Lot-Abfragen
    Task<IList<AssetLot>> GetAvailableLots(int walletId, string symbol);
    Task<IList<AssetLot>> GetAltbestandLots(int walletId, string symbol);
    Task<IList<AssetLot>> GetNeubestandLots(int walletId, string symbol);
    
    // Lot-Verwendung
    Task<LotMovement> UseLotForSale(int lotId, decimal quantity, decimal salePriceEur);
    Task<IList<LotMovement>> TransferLots(int transactionId, IList<LotAllocation> allocations);
    
    // Steuerberechnung
    Task<TaxCalculationResult> CalculateTaxForSale(IList<LotAllocation> allocations, decimal totalSalePriceEur);
}
```

### AP 3: Unverknüpfte Transaktionen erkennen

**Aufwand:** ~4h

```csharp
public interface ITransactionLinkingService
{
    Task<IList<CryptoTransaction>> GetUnlinkedTransactions();
    Task<IList<CryptoTransaction>> GetTransactionsRequiringLotAssignment();
    Task LinkTransactions(int sendId, int receiveId);
}
```

### AP 4: UI – Lot-Zuordnungs-Dialog

**Aufwand:** ~16h

1. Wiederverwendbare Komponente `LotSelector.razor`
2. Integration in:
   - Transfer-Workflow
   - Verkauf-Workflow
   - Unverknüpfte Transaktionen

### AP 5: UI – Unverknüpfte Transaktionen-Dashboard

**Aufwand:** ~8h

1. Neue Seite `/unverknuepft`
2. Liste aller problematischen Transaktionen
3. Wizard zur Behebung

### AP 6: UI – Geldfluss-Visualisierung

**Aufwand:** ~12h

1. Sankey-Diagramm-Komponente (z.B. mit Blazor + Chart.js oder D3.js)
2. Timeline-Ansicht
3. Export als PDF/PNG für Finanzamt

### AP 7: Steuerbericht-Generator

**Aufwand:** ~16h

```csharp
public interface ITaxReportService
{
    Task<TaxReport> GenerateAnnualReport(int year);
    Task<byte[]> ExportToPdf(TaxReport report);
    Task<byte[]> ExportToCsv(TaxReport report);
    Task<FinanzOnlineData> GetFinanzOnlineData(int year);
}
```

### AP 8: Altbestand-Import

**Aufwand:** ~8h

1. CSV-Import für historische Daten
2. Manuelle Erfassung von Altbestand-Lots
3. Validierung der Daten

### AP 9: Tests

**Aufwand:** ~12h

1. Unit Tests für Lot-Service
2. Unit Tests für Steuerberechnung
3. Integration Tests für Workflows

---

## 8. Prioritäten

### Phase 1: Fundament (MVP)
1. **AP 1**: Datenmodell
2. **AP 2**: Lot-Service (Basis)
3. **AP 4**: Lot-Zuordnungs-Dialog

### Phase 2: Compliance
4. **AP 3**: Unverknüpfte Transaktionen
5. **AP 5**: Dashboard für Probleme
6. **AP 7**: Steuerbericht-Generator
7. **AP 8**: Altbestand-Import

### Phase 3: Visualisierung
8. **AP 6**: Geldfluss-Visualisierung
9. **AP 9**: Tests

---

## 9. Offene Fragen

1. **Gleitender Durchschnitt vs. Einzelzuordnung**: 
   - Das BMF schreibt ACB für Assets auf derselben Adresse vor
   - Wie soll das System damit umgehen, wenn User explizite Lots wählen will?
   - Vorschlag: Beide Modi anbieten (ACB-konform vs. feingranular für eigene Dokumentation)

2. **Krypto-zu-Krypto-Tausch**:
   - Obwohl steuerfrei, müssen Anschaffungskosten weitergegeben werden
   - Wie tief soll die Tranchenverfolgung gehen?

3. **DeFi-Transaktionen**:
   - Staking, Liquidity Providing etc. erzeugen komplexe Lot-Strukturen
   - Welche DeFi-Aktivitäten sollen unterstützt werden?

4. **Automatischer KESt-Abzug**:
   - Bitpanda etc. führen seit 2024 KESt automatisch ab
   - Wie soll das System bereits abgeführte KESt tracken?

---

## 10. Nächste Schritte

1. ✅ Plan erstellen und reviewen lassen
2. ⬜ Datenmodell finalisieren (Feedback einarbeiten)
3. ⬜ Migration erstellen und testen
4. ⬜ Lot-Service implementieren
5. ⬜ UI für Lot-Zuordnung bauen
6. ⬜ Steuerbericht-Generator entwickeln

---

*Erstellt: 30.01.2026*
*Version: 1.0*
