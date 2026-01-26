using System.Text;
using CryptoTracker.Services;
using FluentAssertions;

namespace CryptoTracker.Tests.Importers.AutoImport;

public class BitcoinDeAutoImportTests : DbTestBase
{
    private const string WalletName = "BitcoinDeWallet";

    [Fact]
    public async Task ImportAccountStatementCsv()
    {
        var csv = BuildAccountStatementCsv();
        await ImportAsync("btc_account_statement.csv", csv);

        DbContext.CryptoTrades.Should().HaveCount(10);
        DbContext.CryptoTransactions.Should().HaveCount(5);
    }

    [Fact]
    public async Task ImportAccountStatementDotDecimalsCsv()
    {
        var csv = BuildAccountStatementDotDecimalsCsv();
        await ImportAsync("btc_account_statement_20130101-20240127.csv", csv);

        var entry = DbContext.BitcoinDeTransactions.Single(t => t.Referenz == "QW3A9T");
        entry.CryptoNachGebuehr.Should().Be(0.99000000m);
        entry.MengeNachGebuehr.Should().Be(60.69m);
        entry.EinheitMengeNachGebuehr.Should().Be("EUR");
    }

    [Fact]
    public async Task ImportAccountStatementAdjustmentsCsv()
    {
        var csv = BuildAccountStatementAdjustmentsCsv();
        await ImportAsync("btc_account_statement_adjustments.csv", csv);

        DbContext.CryptoTransactions.Should().HaveCount(5);
    }

    [Fact]
    public async Task ImportBuyHistoryCsv()
    {
        var csv = BuildBuyHistoryCsv();
        await ImportAsync("buy_history.csv", csv);

        DbContext.CryptoTrades.Should().HaveCount(20);
        var earliest = DbContext.CryptoTrades.OrderBy(t => t.DateTime).First();
        earliest.DateTime.Month.Should().Be(2);
        earliest.DateTime.Day.Should().Be(1);
        earliest.DateTime.Hour.Should().Be(10);
    }

    [Fact]
    public async Task ImportSellHistoryCsv()
    {
        var csv = BuildSellHistoryCsv();
        await ImportAsync("sell_history.csv", csv);

        DbContext.CryptoTrades.Should().HaveCount(20);
    }

    [Fact]
    public async Task ImportDepositHistoryCsv()
    {
        var csv = BuildDepositHistoryCsv();
        await ImportAsync("deposit_history.csv", csv);

        DbContext.CryptoTransactions.Should().HaveCount(10);
    }

    [Fact]
    public async Task ImportWithdrawalHistoryCsv()
    {
        var csv = BuildWithdrawalHistoryCsv();
        await ImportAsync("withdrawal_history.csv", csv);

        DbContext.CryptoTransactions.Should().HaveCount(10);
    }

    private async Task ImportAsync(string fileName, string csv)
    {
        var dataImportService = new DataImportService(DbContext);
        var autoService = new ImportAutoService(dataImportService);
        await autoService.ImportAsync(WalletName, () => new MemoryStream(Encoding.UTF8.GetBytes(csv)), fileName, null);
    }

    private static string BuildAccountStatementCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Datum;Typ;Währung;Referenz;BTC-Adresse;Kurs;\"Einheit (Kurs)\";\"Crypto vor Gebühr\";\"Menge vor Gebühr\";\"Einheit (Menge vor Gebühr)\";\"Crypto nach Bitcoin.de-Gebühr\";\"Menge nach Bitcoin.de-Gebühr\";\"Einheit (Menge nach Bitcoin.de-Gebühr)\";\"Zu- / Abgang\";Kontostand;Kommentar");
        for (var i = 1; i <= 10; i++)
        {
            if (i <= 5)
            {
                var row = new[]
                {
                    $"2024-01-{i:00} 12:00:00",
                    "Kauf",
                    "BTC",
                    $"REF{i}",
                    $"ADDR{i}",
                    "40000,00",
                    "BTC / EUR",
                    "0,01000000",
                    "400,00",
                    "EUR",
                    "0,00990000",
                    "396,00",
                    "EUR",
                    "0,00990000",
                    "0,00990000",
                    $"Kauf {i}"
                };
                sb.AppendLine(string.Join(';', row));
            }
            else
            {
                var row = new[]
                {
                    $"2024-01-{i:00} 15:00:00",
                    "Auszahlung",
                    "BTC",
                    $"TX{i}",
                    $"ADDR{i}",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "-0,00500000",
                    "0,00490000",
                    $"Auszahlung {i}"
                };
                sb.AppendLine(string.Join(';', row));
            }
        }

        return sb.ToString();
    }

    private static string BuildAccountStatementDotDecimalsCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Datum;Typ;Währung;Referenz;BTC-Adresse;Kurs;\"Einheit (Kurs)\";\"BTC vor Gebühr\";\"Menge vor Gebühr\";\"Einheit (Menge vor Gebühr)\";\"BTC nach Bitcoin.de-Gebühr\";\"Menge nach Bitcoin.de-Gebühr\";\"Einheit (Menge nach Bitcoin.de-Gebühr)\";\"Zu- / Abgang\";Kontostand");
        sb.AppendLine("\"2013-04-17 15:35:15\";Kauf;BTC;QW3A9T;;61.00;\"BTC / EUR\";1.00000000;61.00;EUR;0.99000000;60.69;EUR;0.99000000;0.99000000");
        return sb.ToString();
    }

    private static string BuildAccountStatementAdjustmentsCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Datum;Typ;Währung;Referenz;Adresse;Kurs;Einheit (Kurs);Crypto vor Gebühr;Menge vor Gebühr;Einheit (Menge vor Gebühr);Crypto nach Bitcoin.de-Gebühr;Menge nach Bitcoin.de-Gebühr;Einheit (Menge nach Bitcoin.de-Gebühr);Zu- / Abgang;Kontostand;Kommentar");
        sb.AppendLine("2017-08-01 14:43:14;Initialisierung;BCH;;;;;;;;;;;0.00012929;0.00012929;");
        sb.AppendLine("2017-10-24 03:24:35;Initialisierung;BTG;;;;;;;;;;;0.00012929;0.00012929;");
        sb.AppendLine("2018-03-01 03:30:48;Partnerprogramm;ETH;tradeGH;;;;;;;;;;0.00050000;0.00050000;");
        sb.AppendLine("2018-03-01 04:09:01;Partnerprogramm;BTC;tradeGH;;;;;;;;;;0.00005000;0.00005000;");
        sb.AppendLine("2023-02-09 12:04:55;Korrekturposition;BTC;Gutschrift BTC (Verkauf BSV) laut Abkündigung;;;;;;;;;;0.00000025;0.00000025;");
        return sb.ToString();
    }

    private static string BuildBuyHistoryCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Datum;Symbol;Kurs;Menge;Betrag");
        for (var i = 1; i <= 10; i++)
        {
            sb.AppendLine($"{i:00}.02.2024 10:00;BTC;{40000 + i},00;0,00{i};{400 + i},00");
        }

        return sb.ToString();
    }

    private static string BuildSellHistoryCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Datum;Symbol;Kurs;Menge;Betrag");
        for (var i = 1; i <= 10; i++)
        {
            sb.AppendLine($"{i:00}.03.2024 11:00;BTC;{41000 + i},00;0,00{i};{410 + i},00");
        }

        return sb.ToString();
    }

    private static string BuildDepositHistoryCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Datum;Symbol;Menge");
        for (var i = 1; i <= 10; i++)
        {
            sb.AppendLine($"{i:00}.04.2024 09:00;BTC;0,0{i} BTC");
        }

        return sb.ToString();
    }

    private static string BuildWithdrawalHistoryCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Datum;Symbol;Menge;zzgl. Netzwerk-Gebühr;Kommentar;Adresse");
        for (var i = 1; i <= 10; i++)
        {
            sb.AppendLine($"{i:00}.05.2024 18:00;BTC;0,0{i} BTC;0,000{i} BTC;Test {i};ADDR{i}");
        }

        return sb.ToString();
    }
}
