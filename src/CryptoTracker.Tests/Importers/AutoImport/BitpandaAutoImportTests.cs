using System.Text;
using CryptoTracker.Services;
using FluentAssertions;

namespace CryptoTracker.Tests.Importers.AutoImport;

public class BitpandaAutoImportTests : DbTestBase
{
    private const string WalletName = "BitpandaWallet";

    [Fact]
    public async Task ImportTransactionsCsv()
    {
        var csv = BuildBitpandaCsv();
        await ImportAsync("bitpanda_transactions.csv", csv);

        DbContext.CryptoTrades.Should().HaveCount(4);
        DbContext.CryptoTransactions.Should().HaveCount(5);
    }

    [Fact]
    public async Task ImportTransactionsCsv_DoesNotDuplicateOnReimport()
    {
        var csv = BuildBitpandaCsv();
        await ImportAsync("bitpanda_transactions.csv", csv);
        await ImportAsync("bitpanda_transactions.csv", csv);

        DbContext.CryptoTrades.Should().HaveCount(4);
        DbContext.CryptoTransactions.Should().HaveCount(5);
    }

    private async Task ImportAsync(string fileName, string csv)
    {
        var dataImportService = new DataImportService(DbContext);
        var autoService = new ImportAutoService(dataImportService);
        await autoService.ImportAsync(WalletName, () => new MemoryStream(Encoding.UTF8.GetBytes(csv)), fileName, null);
    }

    private static string BuildBitpandaCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("\"Transaction ID\",Timestamp,\"Transaction Type\",\"In/Out\",\"Amount Fiat\",Fiat,\"Amount Asset\",Asset,\"Asset market price\",\"Asset market price currency\",\"Asset class\",\"Product ID\",Fee,\"Fee asset\",Spread,\"Spread Currency\",\"Tax Fiat\",Address,Comment");
        for (var i = 1; i <= 10; i++)
        {
            if (i <= 3)
            {
                sb.AppendLine($"TX-{i},2024-06-{i:00}T10:00:00+01:00,deposit,incoming,100.00,EUR,-,EUR,-,-,Fiat,-,0.00,EUR,-,-,0.00,,");
            }
            else if (i <= 5)
            {
                sb.AppendLine($"TX-{i},2024-06-{i:00}T11:00:00+01:00,buy,outgoing,100.00,EUR,0.004{i},ETH,2500.00,EUR,Cryptocurrency,5,-,-,-,-,0.00,,");
            }
            else if (i <= 7)
            {
                var inOut = i % 2 == 0 ? "incoming" : "outgoing";
                sb.AppendLine($"TX-{i},2024-06-{i:00}T19:59:41+02:00,transfer,{inOut},2.15,EUR,0.3812{i}557,ETHW,5.63,EUR,Cryptocurrency,2715,-,-,-,-,-");
            }
            else
            {
                sb.AppendLine($"TX-{i},2024-06-{i:00}T12:00:00+01:00,withdrawal,outgoing,0,EUR,0.00{i},ETH,0.00,-,Cryptocurrency,5,0.0001,ETH,-,-,0.00,0xabc{i},Test {i}");
            }
        }

        return sb.ToString();
    }
}
