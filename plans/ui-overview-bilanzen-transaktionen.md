# Plan: UI-Überarbeitung Überblick, Bilanzen, Transaktionen, Kurse

## Ziele
- Überblick: Gesamtsaldo aller Wallets, Anzahl Coins (unique Symbols), Top-3-Coins inkl. Euro-Wert und Menge; Wallets als Kacheln mit Euro-Wert + Anzahl Assets; Klick führt zur Bilanzen-Ansicht mit vorausgewähltem Wallet.
- Bilanzen: Top-Navigator für Wallets; oben Wallet-Gesamtsaldo; darunter Asset-Liste absteigend nach Euro-Wert; Spalten Symbol, Menge, Wert in EUR, Kurs; Klick auf Asset führt zu Transaktionen-Seite (Wallet + Coin vorausgewählt).
- Transaktionen: Top-Navigator für Wallets (Alle + einzelne Wallets) und Coin-Filter (Alle + Coins des gewählten Wallets); Liste aller Transaktionen/Trades aufsteigend nach Datum; Spalten: Typ, Flow (Eingang/Ausgang mit Pfeil+Farbe), Coin, Anzahl, Wert in EUR, Source Wallet, Target Wallet.
- Kurse: Neuer Menüpunkt links; Liste aller Coins in Wallets, aktueller und vorheriger Kurs; Möglichkeit, fehlenden Kurs manuell mit Datum zu setzen (Popup).
- Formatierung: Euro-Symbol überall für Euro-Werte; Zahlenformat: min. 2 Dezimalstellen, danach keine unnötigen Nullen; Zeitspalten als „dd.MM.yyyy HH:mm:ss UTC“.
- Laden: Sichtbare Lade-Visualisierung (z. B. Skeleton/Overlay) statt „plötzliches“ Auftauchen.
- Design: Aufpoliertes Layout (Kacheln, Summary-Header, Top-Liste, Farben/Hintergründe), professioneller Gesamteindruck.

## Offene Fragen / Annahmen
- „Top 3 Coins“: nach Gesamtwert in EUR über alle Wallets; Anzahl Coins = Anzahl unterschiedlicher Symbols. (Bitte bestätigen.)
- „Vorige Kurse“: gemeint ist Vortageskurs (z. B. Schlusskurs des Vortags) oder „letzter bekannter Kurs“? (Bitte klären.)
- CoinMarketCap-Links: Symbol -> Slug Mapping via CMC-API; falls unbekannt, Fallback auf Suchseite.

## Arbeitspakete
1) **API/DTO-Erweiterungen (Backend + Shared DTOs)**
- Neue DTOs für Übersicht/Wallet-Summary (z. B. WalletSummaryDTO, OverviewSummaryDTO, CoinSummaryDTO) unter `src/CryptoTracker.Client/Shared/`.
- Erweiterung/Neuanlage von Endpoints:
  - Overview-Summary: Gesamtsaldo, Coin-Anzahl, Top-3-Coins, Wallet-Kacheldaten.
  - Bilanzen für einzelnes Wallet (inkl. Gesamtwert und sortierte Assets).
  - Transaktionen mit Filtern (Wallet optional, Coin optional), sortiert nach Datum aufsteigend.
  - Kurse: aktuelle + vorherige Kurse je Coin; Setzen eines manuellen Kurses mit Datum.
- Services anpassen/ergänzen:
  - `BalanceService` auf Wallet/Asset- und Gesamtsummen erweitern.
  - `FlowService`/neuer Service für filterbare Transaktionsliste.
  - Neuer Service/Repository für Kurs-Overrides (DB-Entity + Migration).

2) **Routing & Navigation**
- Neue Route für Transaktionen-Seite (z. B. `/transaktionen`).
- Querystring oder Route-Parameter für Vorauswahl (Wallet, Coin) in Bilanzen/Transaktionen.
- Linker Navigator aktualisieren in `src/CryptoTracker/Components/Layout/NavMenu.razor` (neuer Menüpunkt „Kurse“).

3) **Wiederverwendbare UI-Bausteine**
- Top-Navigator-Komponente (Tabs/Chips) für Wallet- und Coin-Auswahl.
- Formatierungs-Helper (z. B. `NumberFormat.cs`) für Euro-/Mengenformat + UTC-Datum.
- CoinMarketCap-Link-Komponente (Symbol -> URL) für alle Coin-Anzeigen.
- Einheitliche Lade-Visualisierung (z. B. Overlay/BusyIndicator-Komponente).

4) **Überblick-Seite** (`src/CryptoTracker.Client/Pages/Overview.razor` + `.cs`)
- Summary-Header: Gesamtsaldo, Coin-Anzahl, Top-3-Coins mit EUR-Wert + Menge.
- Wallet-Kacheln (Kachel-Grid) mit Euro-Wert + Asset-Anzahl; Klick navigiert zu Bilanzen.
- Optional: Transaktionsliste aus Überblick entfernen oder auf eigene Transaktionen-Seite verlagern.

5) **Bilanzen-Seite** (`src/CryptoTracker.Client/Pages/Bilanzen.razor` + `.cs`)
- Top-Navigator für Wallets, standardmäßig Vorauswahl aus Route/Query.
- Gesamtwert oben; Asset-Liste absteigend nach EUR-Wert.
- Spalte „Kurs“ ergänzen (Asset-Preis in EUR).
- Row-Click navigiert zu Transaktionen mit Wallet+Coin.

6) **Transaktionen-Seite** (neu)
- Top-Navigator Wallets (Alle + einzelne).
- Coin-Filter (Alle + Coins des ausgewählten Wallets; bei „Alle“ Wallets alle Coins).
- Grid: Typ, Flow (Icon/Farbe), Coin, Anzahl, Wert in EUR, Source/Target Wallet, Datum (UTC format).
- Sortierung Datum aufsteigend.

7) **Kurse-Seite** (neu)
- Liste aller Coins (aus Wallets) mit aktuellem & vorherigem Kurs.
- Fehlende Kurse markieren; Klick auf Zeile öffnet Popup zum Setzen (EUR + Datum, Default heute).
- Speicherung im Backend; Anzeige/Verwendung in Balance-/Overview-Berechnungen als Fallback, wenn Provider keinen Kurs liefert.

8) **Design & Styling**
- `src/CryptoTracker/wwwroot/app.css` erweitern:
  - Kachel-Grid, Summary-Header, Top-3-Liste, Top-Navigator-Stil.
  - Farben/Abstände/Typo, dezente Hintergründe für Karten (nicht weiß).
  - Tabellen/Grid-Optik für bessere Lesbarkeit.
- Responsive Layout (Desktop + Mobile).

9) **Tests**
- Unit-Tests in `src/CryptoTracker.Tests` für:
  - Summary-Berechnungen (Top-3, Totals, Coin-Anzahl).
  - Kurs-Fallback (Provider fehlt -> Override).
  - Transaktionsfilter (Wallet/Coin).

## Lieferkriterien
- Überblick, Bilanzen, Transaktionen und Kurse entsprechen UI/UX-Vorgaben.
- Formatierung konsistent (Euro-Symbol, Rundung, UTC-Zeitformat).
- CoinMarketCap-Links überall verfügbar.
- Lade-Visualisierung sichtbar.
- Tests grün.
