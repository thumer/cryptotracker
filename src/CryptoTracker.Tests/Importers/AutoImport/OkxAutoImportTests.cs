using System.Text;
using CryptoTracker.Entities;
using CryptoTracker.Services;
using FluentAssertions;

namespace CryptoTracker.Tests.Importers.AutoImport;

public class OkxAutoImportTests : DbTestBase
{
    private const string WalletName = "OkxWallet";

    [Fact]
    public async Task ImportDepositCsv()
    {
        var csv = BuildOkxDepositCsv();
        await ImportAsync("okx_deposit_history.csv", csv);

        DbContext.CryptoTransactions.Should().HaveCount(10);
        DbContext.CryptoTransactions.Should().OnlyContain(t => t.TransactionType == TransactionType.Receive);
    }

    [Fact]
    public async Task ImportTradeCsv()
    {
        var csv = BuildOkxTradeCsv();
        await ImportAsync("okx_trading_history.csv", csv);

        DbContext.CryptoTrades.Should().HaveCount(20);
    }

    private async Task ImportAsync(string fileName, string csv)
    {
        var dataImportService = new DataImportService(DbContext);
        var autoService = new ImportAutoService(dataImportService);
        await autoService.ImportAsync(WalletName, () => new MemoryStream(Encoding.UTF8.GetBytes(csv)), fileName, null);
    }

    private static string BuildOkxDepositCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date(UTC);Coin;Network;Amount;Address;Kommentar");
        for (var i = 1; i <= 10; i++)
        {
            sb.AppendLine($"{i:00}.09.2024 10:00;ETH;ETH;0,{i}5;0xabc{i};OKX Deposit {i}");
        }

        return sb.ToString();
    }

    private static string BuildOkxTradeCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date(UTC);Pair;Side;Price;Executed");
        for (var i = 1; i <= 10; i++)
        {
            var side = i % 2 == 0 ? "SELL" : "BUY";
            sb.AppendLine($"{i:00}.10.2024 12:00;ADAUSDT;{side};0,{i}5 USDT;{i}");
        }

        return sb.ToString();
    }
}
