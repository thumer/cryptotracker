# Repository Guidelines

## Projektstruktur & Modulorganisation
- `CryptoTracker.slnx` ist der Einstiegspunkt der Solution.
- `src/CryptoTracker` enthält den ASP.NET Core Host (API-Controller, Services, EF Core `DbContext`, `Migrations/` und geteilte UI-Komponenten).
- `src/CryptoTracker.Client` ist die Blazor WebAssembly UI (Seiten unter `Pages/`, gemeinsame UI unter `Shared/`, statische Assets in `wwwroot/`).
- `src/CryptoTracker.Tests` enthält xUnit-Tests (Importer-Tests liegen in `Importers/`).
- `infra/` beinhaltet Azure-Deployment (Bicep-Templates).
- `src/TestApp` ist eine lokale Sandbox; Experimente bitte dort isolieren.

## Build-, Test- und Dev-Kommandos
- `dotnet restore CryptoTracker.slnx` — stellt NuGet-Abhängigkeiten wieder her.
- `dotnet build CryptoTracker.slnx --configuration Release` — CI-konformer Build.
- `dotnet run --project src/CryptoTracker` — lokale Ausführung (Profile in `src/CryptoTracker/Properties/launchSettings.json`).
- `dotnet test CryptoTracker.slnx --configuration Release` — führt die Testsuite aus.

## Coding-Style & Namenskonventionen
- C# 10+ Konventionen: 4 Leerzeichen, file-scoped namespaces, Nullable References aktiviert.
- Naming: `PascalCase` für Typen/Methoden, `camelCase` für lokale Variablen/Parameter, `_camelCase` für private Felder.
- DTOs und Entities liegen unter `Shared/` bzw. `Entities/`; vorhandene Muster übernehmen (z. B. `WalletDTO`, `CryptoTrade`).
- Blazor-Komponenten als `.razor`-Dateien, Dateiname entspricht der Komponente (z. B. `Wallets.razor`).
- Dokumentationstexte verwenden Umlaute als äöü (nicht ae/oe/ue).

## Testing-Richtlinien
- Frameworks: xUnit + FluentAssertions; EF Core InMemory wird in Tests genutzt.
- Testdateien folgen `*Tests.cs` und liegen unter `src/CryptoTracker.Tests`.
- Bevorzugt fokussierte Unit-Tests für Importer und Services; Testdaten inline halten.

## Commit- & Pull-Request-Richtlinien
- Commit-Messages sind kurz, im Imperativ und schlicht (z. B. „Add wallet DTO“, „Improve asset flow view“).
- PRs enthalten eine kurze Zusammenfassung, Test-Resultate und Screenshots bei UI-Änderungen.
- Relevante Issues verlinken; CI Build/Test muss grün sein.

## Konfiguration & Secrets
- Die API erwartet `ConnectionStrings:DefaultConnection` (SQL Server). Für lokal User-Secrets nutzen, z. B.
  `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;Database=...;Trusted_Connection=True;"`
