using System.Text;
using CryptoTracker.Entities;
using CryptoTracker.Services;
using FluentAssertions;

namespace CryptoTracker.Tests.Importers.AutoImport;

public class MetamaskAutoImportTests : DbTestBase
{
    private const string WalletName = "MetamaskWallet";

    [Fact]
    public async Task ImportTransactionsCsv()
    {
        var csv = BuildMetamaskTransactionsCsv();
        await ImportAsync("metamask_transactions.csv", csv);

        DbContext.CryptoTransactions.Should().HaveCount(10);
        DbContext.CryptoTransactions.Count(t => t.TransactionType == TransactionType.Receive).Should().Be(5);
        DbContext.CryptoTransactions.Count(t => t.TransactionType == TransactionType.Send).Should().Be(5);
    }

    [Fact]
    public async Task ImportTradesCsv()
    {
        var csv = BuildMetamaskTradesCsv();
        await ImportAsync("metamask_trades.csv", csv);

        DbContext.CryptoTrades.Should().HaveCount(20);
    }

    private async Task ImportAsync(string fileName, string csv)
    {
        var dataImportService = new DataImportService(DbContext);
        var autoService = new ImportAutoService(dataImportService);
        await autoService.ImportAsync(WalletName, () => new MemoryStream(Encoding.UTF8.GetBytes(csv)), fileName, null);
    }

    private static string BuildMetamaskTransactionsCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Datum;Typ;Coin;Network;Amount;TransactionFee;Kommentar");
        for (var i = 1; i <= 10; i++)
        {
            var typ = i % 2 == 0 ? "Ausgang" : "Eingang";
            sb.AppendLine($"{i:00}.07.2024 12:{i:00};{typ};ETH;BSC;0,{i}00000;0,000{i};Metamask {i}");
        }

        return sb.ToString();
    }

    private static string BuildMetamaskTradesCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date(UTC);Pair;Side;Price;Executed;Amount;Fee;Tradingplatform");
        for (var i = 1; i <= 10; i++)
        {
            var side = i % 2 == 0 ? "SELL" : "BUY";
            sb.AppendLine($"{i:00}.08.2024;ETH-USDT;{side};0,{i}5 USDT;0,00{i} ETH;0,{i}0 USDT;0,000{i} ETH;uniswap");
        }

        return sb.ToString();
    }
}
