# Lot Flow Tracking - Dokumentation

## Übersicht

Das Lot Flow Tracking System ermöglicht die vollständige Nachverfolgung von Krypto-Assets von ihrer Anschaffung bis zum Verkauf. Dies ist für die österreichische Steuerberechnung (KESt 27.5%) essentiell, da nur Lots mit **vollständig nachvollziehbarem Flow** für steuerliche Zwecke verwendet werden dürfen.

### Warum Flow-Tracking?

Ein Lot kann nur für die Steuerberechnung verwendet werden, wenn seine gesamte Kette nachvollziehbar ist:

- **Beispiel 1:** Du kaufst 1 BTC mit EUR → Swap zu ETH → Swap zurück zu 0.8 BTC  
  Das ursprüngliche Lot zeigt noch 1 BTC, aber du hast nur 0.8 BTC. Ohne Flow-Tracking wäre die Steuerbasis falsch.

- **Beispiel 2:** Externe Einzahlung von 2 ETH ohne Herkunftsnachweis  
  Dieses Lot kann nicht für steuerliche Zwecke verwendet werden, da die Anschaffungskosten unbekannt sind.

---

## Kernkonzepte

### 1. AssetLot (Steuerliches Losmodell)

Ein `AssetLot` repräsentiert eine steuerliche Einheit eines Krypto-Assets mit:

| Feld | Beschreibung |
|------|--------------|
| `AcquisitionDate` | Anschaffungsdatum - wichtig für Altbestand (vor 01.03.2021) |
| `AcquisitionCostEur` | Anschaffungskosten in EUR - Basis für Gewinn/Verlust |
| `OriginalQuantity` | Ursprüngliche Menge |
| `RemainingQuantity` | Verbleibende Menge (nach Teil-Verkäufen) |
| `AcquisitionType` | Herkunftstyp (FiatPurchase, CryptoSwap, etc.) |

### 2. Flow-Status Felder

| Feld | Beschreibung |
|------|--------------|
| `IsFlowComplete` | Ist die Kette bis zum Ursprung vollständig nachvollziehbar? |
| `FlowIncompleteReason` | Grund für unvollständigen Flow (falls zutreffend) |
| `TransformedToLotId` | Bei Swap: Zeigt auf das neue Lot (Ziel) |
| `TransformedFromLots` | Bei Swap: Collection der Source-Lots (Quellen) |
| `ParentLotId` | Bei Transfer: Zeigt auf das ursprüngliche Lot |

---

## Herkunftstypen (AcquisitionType)

| Typ | Flow-Status | Beschreibung |
|-----|-------------|--------------|
| `FiatPurchase` | ✅ Immer vollständig | Kauf mit EUR/USD - Ursprung der Kette |
| `Manual` | ✅ Immer vollständig | Manuell eingetragen - User übernimmt Verantwortung |
| `Mining` | ✅ Immer vollständig | Mining-Reward - Anschaffung zu Zeitwert |
| `Staking` | ✅ Immer vollständig | Staking-Reward - Anschaffung zu Zeitwert |
| `Lending` | ✅ Immer vollständig | Lending-Zinsen - Anschaffung zu Zeitwert |
| `Airdrop` | ✅ Immer vollständig | Airdrop - Anschaffung zu Zeitwert |
| `Hardfork` | ✅ Immer vollständig | Hardfork-Coins - Anschaffung zu Zeitwert |
| `Gift` | ✅ Immer vollständig | Schenkung - Anschaffung zu Zeitwert |
| `InternalTransfer` | 🔗 Abhängig | Vollständig wenn ParentLot vollständig UND OppositeTransaction existiert |
| `CryptoSwap` | 🔗 Abhängig | Vollständig wenn OppositeTrade existiert UND alle Source-Lots vollständig |
| `ExternalDeposit` | ❌ Immer unvollständig | Externe Einzahlung ohne nachweisbare Herkunft |

---

## Entity-Beziehungen

### CryptoTrade (Käufe/Verkäufe/Swaps)

```
┌─────────────────────────────────────────────────────────────┐
│ CryptoTrade                                                 │
├─────────────────────────────────────────────────────────────┤
│ - OppositeTradeId → CryptoTrade    (Swap-Partner)           │
│ - SourceLotId → AssetLot           (Bei Sell: verwendetes Lot) │
│ - ResultingLotId → AssetLot        (Bei Buy: erstelltes Lot)│
│ - LotMovements → [LotMovement]     (Lot-Allokationen)       │
└─────────────────────────────────────────────────────────────┘
```

**Verwendung:**
- **Fiat-Kauf:** `ResultingLotId` zeigt auf das neue Lot
- **Fiat-Verkauf:** `LotMovements` dokumentiert welche Lots verwendet wurden
- **Crypto-Swap (Sell-Seite):** `SourceLotId` zeigt auf das verwendete Lot
- **Crypto-Swap (Buy-Seite):** `ResultingLotId` zeigt auf das neue Lot

### CryptoTransaction (Wallet-Transfers)

```
┌─────────────────────────────────────────────────────────────┐
│ CryptoTransaction                                           │
├─────────────────────────────────────────────────────────────┤
│ - OppositeTransactionId → CryptoTransaction (Transfer-Partner) │
│ - ResultingLotId → AssetLot        (Bei Receive: erstelltes Lot) │
│ - LotMovements → [LotMovement]     (Lot-Allokationen)       │
└─────────────────────────────────────────────────────────────┘
```

**Verwendung:**
- **Send:** `LotMovements` dokumentiert welche Lots gesendet wurden
- **Receive:** `ResultingLotId` zeigt auf das neue Lot
- **Verknüpfung:** `OppositeTransactionId` verbindet Send ↔ Receive

### AssetLot (Lot-Verknüpfungen)

```
┌─────────────────────────────────────────────────────────────┐
│ AssetLot                                                    │
├─────────────────────────────────────────────────────────────┤
│ - ParentLotId → AssetLot           (Transfer: Ursprungslot) │
│ - TransformedToLotId → AssetLot    (Swap: Ziellot)          │
│ - TransformedFromLots → [AssetLot] (Swap: Quelllots)        │
│ - SourceTradeId → CryptoTrade      (Herkunfts-Trade)        │
│ - SourceTransactionId → CryptoTransaction (Herkunfts-Tx)    │
└─────────────────────────────────────────────────────────────┘
```

### Unterschied: Trade vs Transaction Verknüpfung

| Szenario | Entity | Verknüpfung | Lot-Verknüpfung |
|----------|--------|-------------|-----------------|
| **Wallet Transfer** | `CryptoTransaction` | `OppositeTransactionId` | `ParentLotId` (neues Lot erbt vom alten) |
| **Crypto Swap** | `CryptoTrade` | `OppositeTradeId` | `SourceLotId` + `TransformedFromLots` |

Bei **Transactions** nutzen wir `OppositeTransaction` automatisch für die Flow-Validierung und `ParentLotId` für die Lot-Kette.

Bei **Trades** brauchen wir `SourceLotId` zusätzlich, weil Swaps komplexer sind (mehrere Source-Lots können in ein neues Lot transformiert werden).

---

## Ablauf-Diagramme

### 1. Fiat-Kauf (Ursprung)

```
┌─────────────────┐            ┌─────────────────┐
│ Trade (Buy)     │            │ Lot #1          │
│ 1.0 BTC         │───────────▶│ 1.0 BTC         │
│ Price: €50.000  │ Resulting  │ AcquisitionCost │
│ OppositeSymbol: │ LotId      │ = €50.000       │
│ EUR             │            │ Flow: ✅        │
└─────────────────┘            └─────────────────┘

Lot #1:
  - AcquisitionType = FiatPurchase
  - AcquisitionDate = Trade.DateTime
  - IsFlowComplete = true (Ursprung - immer vollständig)
```

### 2. Wallet Transfer (Intern)

```
Wallet A                              Wallet B
┌──────────┐                         ┌──────────┐
│ Lot #1   │                         │ Lot #2   │
│ 1.0 BTC  │                         │ 1.0 BTC  │
│ Flow: ✅ │                         │ Flow: ✅ │
└────┬─────┘                         └────▲─────┘
     │                                    │
     │ Send-Transaction                   │ Receive-Transaction
     │ (LotMovement: Outflow)             │ (ResultingLotId)
     │                                    │
     └────────────────────────────────────┘
              OppositeTransactionId
```

**Lot #2 Eigenschaften:**
- `ParentLotId` = #1
- `AcquisitionType` = InternalTransfer
- `AcquisitionDate` = von Lot #1 übernommen
- `AcquisitionCostEur` = von Lot #1 übernommen
- `IsFlowComplete` = true (wenn Lot #1 vollständig UND OppositeTransaction existiert)

### 3. Crypto-to-Crypto Swap

```
                    OppositeTrade
         ┌────────────────────────────────┐
         │                                │
         ▼                                │
┌─────────────────┐            ┌─────────────────┐
│ Trade #1 (Sell) │            │ Trade #2 (Buy)  │
│ 1.0 BTC         │            │ 15.0 ETH        │
│ SourceLotId=#1  │            │ ResultingLotId=#2│
└────────┬────────┘            └────────▲────────┘
         │                              │
         │                              │
┌────────▼────────┐            ┌────────┴────────┐
│ Lot #1 (Source) │            │ Lot #2 (Result) │
│ BTC             │───────────▶│ ETH             │
│ RemainingQty: 0 │ Transformed│ TransformedFrom │
│                 │   ToLotId  │ Lots = [#1]     │
└─────────────────┘            └─────────────────┘
```

**Lot #2 Eigenschaften:**
- `AcquisitionType` = CryptoSwap
- `AcquisitionDate` = frühestes Datum aus Source-Lots (wichtig für Altbestand!)
- `AcquisitionCostEur` = gewichteter Durchschnitt aus Source-Lots
- `TransformedFromLots` = [Lot #1]
- `IsFlowComplete` = true (wenn alle Source-Lots vollständig UND OppositeTrade existiert)

**Lot #1 wird aktualisiert:**
- `TransformedToLotId` = #2
- `RemainingQuantity` = 0

### 4. Verkauf gegen Fiat

```
┌─────────────────┐            ┌─────────────────┐
│ Lot #1          │            │ Trade (Sell)    │
│ 1.0 BTC         │───────────▶│ 1.0 BTC         │
│ AcquisitionCost │ LotMovement│ Price: €60.000  │
│ = €50.000       │ (Outflow)  │ OppositeSymbol: │
│ Flow: ✅        │            │ EUR             │
└─────────────────┘            └─────────────────┘
```

**Steuerberechnung (nur wenn `IsFlowComplete = true`):**
- Verkaufserlös: €60.000
- Anschaffungskosten: €50.000
- Gewinn: €10.000
- KESt (27.5%): €2.750

---

## Flow-Validierung (LotFlowValidator)

### Validierungslogik

```
ValidateLotFlowAsync(lotId)
│
├── FiatPurchase / Manual / Mining / Staking / etc.
│   └── ✅ IsComplete = true (Ursprung - keine weitere Prüfung nötig)
│
├── InternalTransfer
│   ├── ParentLotId vorhanden?
│   │   └── Nein → ❌ "Interner Transfer ohne Parent-Lot"
│   ├── SourceTransaction.OppositeTransactionId vorhanden?
│   │   └── Nein → ❌ "Transfer-Transaktion hat keine Gegentransaktion"
│   └── Rekursiv: ParentLot validieren
│       └── ParentLot.IsComplete? → ✅ / ❌
│
├── CryptoSwap
│   ├── SourceTradeId vorhanden?
│   │   └── Nein → ❌ "Crypto-Swap hat keinen Source-Trade"
│   ├── SourceTrade.OppositeTradeId vorhanden?
│   │   └── Nein → ❌ "Swap-Trade hat keinen Gegentrade"
│   ├── TransformedFromLots oder ParentLot vorhanden?
│   │   └── Nein → ❌ "Crypto-Swap hat keine Source-Lots"
│   └── Rekursiv: Alle Source-Lots validieren
│       └── Alle Source-Lots vollständig? → ✅ / ❌
│
└── ExternalDeposit
    └── ❌ "Externe Einzahlung ohne vollständige Herkunftsdokumentation"
```

### Zirkuläre Referenzen

Der Validator erkennt und verhindert zirkuläre Referenzen (z.B. Lot A → Lot B → Lot A).

---

## Services

### LotService

| Methode | Beschreibung |
|---------|--------------|
| `GetAvailableLotsAsync(symbol, walletId, onlyCompleteFlow)` | Verfügbare Lots für ein Asset abrufen |
| `GetAllAvailableLotsWithFlowStatusAsync(symbol)` | Alle Lots mit Flow-Status |
| `AllocateLotForSaleAsync(lotId, quantity, tradeId)` | Lot für Verkauf allokieren |
| `TransferLotAsync(lotId, quantity, fromTx, toTx)` | Lot zwischen Wallets transferieren |
| `TransformLotsViaSwapAsync(request)` | Lots über Crypto-Swap transformieren |

### LotFlowValidator

| Methode | Beschreibung |
|---------|--------------|
| `ValidateLotFlowAsync(lotId)` | Einzelnes Lot validieren |
| `ValidateAndUpdateAllLotsAsync()` | Alle Lots validieren und `IsFlowComplete` aktualisieren |
| `GetLotsWithIncompleteFlowAsync(walletId?)` | Lots mit unvollständigem Flow abrufen |

---

## API-Endpunkte

| Methode | Endpunkt | Beschreibung |
|---------|----------|--------------|
| GET | `/api/lots` | Alle Lots abrufen (optional mit Filter) |
| GET | `/api/lots/{id}` | Einzelnes Lot abrufen |
| GET | `/api/lots/available/{symbol}` | Verfügbare Lots für ein Symbol |
| GET | `/api/lots/flow/{lotId}` | Flow-Status eines Lots validieren |
| POST | `/api/lots/flow/revalidate` | Alle Lots neu validieren |
| GET | `/api/lots/incomplete-flow` | Lots mit unvollständigem Flow |
| POST | `/api/lots/transform-swap` | Lots über Swap transformieren |
| POST | `/api/lots/allocate` | Lot für Verkauf allokieren |

---

## UI-Komponenten

### LotSelector.razor

Ermöglicht die Auswahl von Lots für Verkäufe/Swaps.

**Features:**
- Zeigt Flow-Status-Badge für jedes Lot (✅ Vollständig / ⚠️ Unvollständig)
- Deaktiviert Lots mit unvollständigem Flow standardmäßig
- `AllowIncompleteFlow` Parameter für manuelle Override (mit Warnung)
- FIFO-Vorschlag berücksichtigt nur vollständige Lots
- Sortierung: Vollständige Lots zuerst, dann nach Datum

**CSS-Klassen:**
- `.lot-flow-incomplete` - Grau hinterlegt, gestreifter Hintergrund
- `.lot-badge-flow-complete` - Grünes Badge
- `.lot-badge-flow-incomplete` - Gelbes Badge
- `.flow-warning-message` - Warnhinweis bei unvollständigem Flow

---

## Datenbank-Migrationen

| Migration | Beschreibung |
|-----------|--------------|
| `20260130220034_AddAssetLotTracking` | Basis-Lot-System (AssetLot, LotMovement) |
| `20260131122534_AddLotFlowTracking` | Flow-Tracking Erweiterung (IsFlowComplete, TransformedToLotId, SourceLotId) |

---

## Nächste Schritte (Automatik für Verknüpfungen)

Die folgenden Features sollten als nächstes implementiert werden:

### 1. Automatische Lot-Erstellung bei Fiat-Kauf
Wenn ein Trade mit `OppositeSymbol = EUR/USD` erstellt wird, automatisch ein Lot erstellen.

### 2. Automatische Lot-Verknüpfung bei gepaartem Transfer
Wenn eine `CryptoTransaction` mit `OppositeTransactionId` erstellt wird:
- Send-Seite: `LotMovement` erstellen
- Receive-Seite: Neues Lot mit `ParentLotId` erstellen

### 3. Automatische Swap-Transformation bei gepaartem Trade
Wenn zwei `CryptoTrade`s über `OppositeTradeId` verknüpft werden:
- Sell-Seite: `SourceLotId` setzen
- Buy-Seite: Neues Lot mit `TransformedFromLots` erstellen

### 4. Batch-Revalidierung nach Import
Nach jedem Daten-Import automatisch `ValidateAndUpdateAllLotsAsync()` aufrufen.

### 5. UI für manuelle Lot-Zuweisung
Für externe Einzahlungen ohne automatische Verknüpfung eine UI bereitstellen, um Lots manuell als "Manual" zu markieren.

---

## Glossar

| Begriff | Beschreibung |
|---------|--------------|
| **Lot** | Steuerliche Einheit eines Krypto-Assets mit Anschaffungsdatum und -kosten |
| **Flow** | Die Kette von Transaktionen/Trades von der Anschaffung bis zum aktuellen Zustand |
| **FIFO** | First-In-First-Out - Steuerliche Veräußerungsreihenfolge |
| **Altbestand** | Assets vor 01.03.2021 angeschafft (steuerfrei in Österreich) |
| **KESt** | Kapitalertragsteuer (27.5% in Österreich) |
| **Swap** | Tausch Crypto-to-Crypto (z.B. BTC → ETH) |
| **Transfer** | Bewegung zwischen eigenen Wallets (kein steuerliches Ereignis) |
