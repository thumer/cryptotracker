using System.Text;
using CryptoTracker.Entities;
using CryptoTracker.Services;
using FluentAssertions;

namespace CryptoTracker.Tests.Importers.AutoImport;

public class CexIoAutoImportTests : DbTestBase
{
    private const string WalletName = "CexWallet";

    [Fact]
    public async Task ImportWithdrawalsTxt()
    {
        var csv = BuildCexIoWithdrawals();
        await ImportAsync("cex.io_withdrawals.txt", csv);

        DbContext.CryptoTransactions.Should().HaveCount(10);
        DbContext.CryptoTransactions.Should().OnlyContain(t => t.TransactionType == TransactionType.Send);
        DbContext.CryptoTransactions.Should().OnlyContain(t => t.Symbol == "BTC");
    }

    private async Task ImportAsync(string fileName, string csv)
    {
        var dataImportService = new DataImportService(DbContext);
        var autoService = new ImportAutoService(dataImportService);
        await autoService.ImportAsync(WalletName, () => new MemoryStream(Encoding.UTF8.GetBytes(csv)), fileName, null);
    }

    private static string BuildCexIoWithdrawals()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date;Amount;WalletAddress;Comment");
        for (var i = 1; i <= 10; i++)
        {
            sb.AppendLine($"{i:00}.12.2013 07:{i:00};{i * 0.01m:0.####};1E9u5Jv8ZNqgcu85njMysbhg657MNw1EX{i};Private Wallet?");
        }

        return sb.ToString();
    }
}
