using System.Text;
using CryptoTracker.Entities;
using CryptoTracker.Services;
using FluentAssertions;

namespace CryptoTracker.Tests.Importers.AutoImport;

public class LedgerAutoImportTests : DbTestBase
{
    private const string WalletName = "LedgerWallet";

    [Fact]
    public async Task ImportTransactionsCsv()
    {
        var csv = BuildLedgerTransactionsCsv();
        await ImportAsync("ledger_transaction_history.csv", csv);

        DbContext.CryptoTransactions.Should().HaveCount(6);
        DbContext.CryptoTransactions.Count(t => t.TransactionType == TransactionType.Receive).Should().Be(3);
        DbContext.CryptoTransactions.Count(t => t.TransactionType == TransactionType.Send).Should().Be(3);
        DbContext.LedgerTransactions.Should().HaveCount(6);
    }

    private async Task ImportAsync(string fileName, string csv)
    {
        var dataImportService = new DataImportService(DbContext);
        var autoService = new ImportAutoService(dataImportService);
        await autoService.ImportAsync(WalletName, () => new MemoryStream(Encoding.UTF8.GetBytes(csv)), fileName, null);
    }

    private static string BuildLedgerTransactionsCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Datum (UTC);Typ;Coin;Network;Address;Amount;TransactionFee;Kommentar");
        for (var i = 1; i <= 6; i++)
        {
            var typ = i % 2 == 0 ? "Ausgang" : "Eingang";
            sb.AppendLine($"{i:00}.07.2024 12:0{i};{typ};ETH;BSC;0x{i};0,{i}00000;0,000{i};Ledger {i}");
        }

        return sb.ToString();
    }
}
