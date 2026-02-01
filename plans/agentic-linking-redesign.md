# Agentic Linking Redesign (Transaction + Lot)

## Ziel & Umfang
Dieser Plan beschreibt den Umbau des Transaction‑Linking und Lot‑Linking in einen agentischen Workflow mit wiederholten Agent‑Runs, Session‑Kontext und menschlicher Bestätigung nur bei Unsicherheit. Der LLM ersetzt die bisherige heuristische Auto‑Link‑Logik. Lot‑Linking wird vollständig neu aufgebaut und stellt eine durchgängige Lot‑Verkettung bis zu Root‑Lots sicher.

## Agent-Framework Quellcode
- Lokaler Pfad: `D:\Src\agent-framework`

## Leitprinzipien
- **Agentisch, iterativ:** Der Agent startet selbstständig, stellt bei Bedarf Fragen, arbeitet nach Antworten weiter.
- **Kontextbewusst:** Entscheidungen aus der Session fließen in den Verlauf ein (Session‑History), Persistenz nur bei expliziter Zustimmung.
- **Minimal‑UI‑Interaktion:** Der Benutzer wird nur bei Unsicherheit eingebunden.
- **Keine Hardcodings:** Keine fixen Mapping‑Listen (z. B. „Staking Rewards“). Muster werden gelernt.
- **Lot‑Tracing vollständig:** Jede Transaktion muss zu einem Ursprungslot zurückverfolgbar sein.

---

# Teil A: Technischer Plan für den neuen Agenten (Transaction Linking)

## 1) Architektur‑Übersicht
- **AgentSession** (neu): Zustand für Konversation, offene Fragen, Fortschritt, Abbruch.
- **AgentRunner** (neu): Orchestriert wiederholte `agent.RunAsync`‑Aufrufe, übernimmt Frage‑/Antwort‑Loop.
- **Session‑Store** (bestehendes Dictionary erweitern): Speichert aktive Session + History.
- **Tool‑Events → SignalR**: Tool‑Calls erzeugen Events für UI (linked, marked_external, question, progress, rule_learned, error).

## 2) Session‑Flow (Transaction)
1. Session starten → Agent initialisiert Kontext (Regeln + unverknüpfte TX).
2. Agent arbeitet autonom, ruft Tools auf (link, mark_unlinked, etc.).
3. Bei Unsicherheit erzeugt der Agent eine Frage mit Optionen **+ „Andere Option (Freitext)“**.
4. Benutzerantwort wird in Session‑History aufgenommen → Agent läuft erneut weiter.
5. `save_memory` **nur** wenn `ShouldRemember == true`.

## 3) Question‑Schema (verbindlich)
- **Multiple‑Choice** ist Standard.
- Immer zusätzlich: **„Andere Option (Freitext)“**.
- Freitext wird als Kontext‑Hint dem Agenten übergeben.

## 4) Memory‑Gating
- Entscheidungen innerhalb der Session sind **temporär**.
- Persistenz (`save_memory`) erfolgt **nur** bei User‑Zustimmung.
- Kein Speichern von Heuristik‑Listen im Prompt.

## 5) Auto‑Link‑Ersatz
- Heuristische Auto‑Link‑Logik wird entfernt.
- **LLM entscheidet** bei hoher Konfidenz selbst (automatisches `link_transactions`/`mark_intentionally_unlinked`).
- Unsicher → Frage an den Benutzer.

## 6) Notwendige Änderungen (Technik)
- **AgentDefinition**: System‑Prompt vereinfachen, keine festen Regeln.
- **InteractiveLinkingService**: Agent‑Loop + Session‑History + Pending‑Question.
- **DTOs**: Frage‑/Antwort‑Erweiterung (Freitext‑Option, ggf. Tool‑Meta).
- **UI**: Freitext‑Option fix in jeder Frage.

## 7) Tests
- Session‑State‑Machine (Start → Question → Answer → Continue → Complete).
- Memory‑Gating (`ShouldRemember` true/false).
- Agent‑Loop: Antwort führt zu erneutem `RunAsync`.

---

# Teil B: Lot‑System (Agentisch, vollständige Lot‑Verkettung)

## 1) Zielbild
- Lot‑Linking wird vollständig agentisch und interaktiv.
- **Root‑Lots** entstehen automatisch aus Fiat‑Käufen.
- Keine Namensabfrage im Wizard; Benutzer benennt Root‑Lots später in der Lot‑Verwaltung.
- **Child‑Lots** dienen nur dem Tracing.

## 2) Zentrale Regeln
- **Root‑Lot‑Quelle:** Fiat‑Crypto‑Käufe sind Ursprung.
- **Transfers:** Benutzer entscheidet bei Splits (z. B. 3 ETH Neubestand + 2 ETH Altbestand).
- **Trades (Crypto‑zu‑Crypto):** Lot‑Anteile übertragen proportional in neues Asset.
- **Sells:** Benutzer entscheidet, aus welchen Root‑Lots verkauft wird.
- **Vollständige Verkettung:** Jede Transaktion führt zu einem Root‑Lot.

## 3) Lot‑Agent Tool‑Suite (neu/erweitert)
- `get_pending_assignments` (Receive/Trade/Sell)
- `get_lot_options` (verfügbare Lots + Mengen)
- `allocate_lots` (Zuweisung/Split)
- `create_root_lot` (aus Fiat‑Kauf)
- `transfer_lot_chain` (Transfer‑Propagation)
- `link_trade_lots` (Crypto‑zu‑Crypto)

## 4) Lot‑Session‑Flow
1. Session starten → Pending Assignments laden.
2. Agent analysiert (Symbol, Wallet, Historie, vorhandene Lots).
3. Falls eindeutig → automatische Zuordnung.
4. Falls unklar → Frage mit Lot‑Optionen + Freitext‑Option.
5. User‑Split wird in Lot‑Kette eingetragen.
6. Agent fährt fort.

## 5) UI‑Änderungen (Lot‑Wizard)
- Keine Preis‑Eingabe mehr.
- Lot‑Auswahl/Split UI: Auswahl mehrerer Lots + Mengen.
- Freitext‑Option als Hint.
- Live‑Events: assigned, lot_created, question, rule_learned.

## 6) Datenmodell
- Bestehende `AssetLot`‑Struktur bleibt.
- Root‑Lots bleiben ohne Parent.
- Child‑Lots übernehmen Parent‑Referenz für Tracing.
- Optional: „RootLotName“ nur in Verwaltung setzen.

## 7) Tests
- Root‑Lot‑Erstellung aus Fiat‑Kauf.
- Split‑Logik (Transfer + Trade + Sell).
- Vollständige Verkettung bis Root‑Lot.

---

## Entscheidungen zu offenen Punkten
- **Unklare Splits:** immer fragen (kein Default‑FIFO bei Unsicherheit).
- **Split‑UI:** Eingabe sowohl in absoluten Mengen als auch in Prozenten.
- **Zusatz‑Events:** Agent sendet Zwischenerklärungen/Status‑Events; nach User‑Antwort arbeitet er weiter.

---

## Umsetzungsschritte (high‑level)
1. Gemeinsames Agent‑Session‑Framework bauen.
2. Transaction‑Linking umstellen (Agent‑Loop + UI‑Fragen + Memory‑Gating).
3. Lot‑Linking‑Agent + Tools implementieren.
4. UI‑Wizard‑Anpassungen.
5. Tests + Migration der Logik.
