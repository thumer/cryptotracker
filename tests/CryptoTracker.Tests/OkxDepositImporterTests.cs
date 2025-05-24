using CryptoTracker.Entities;
using CryptoTracker.Import;
using FluentAssertions;

namespace CryptoTracker.Tests;

public class OkxDepositImporterTests
{
    private const string Wallet = "Main";

    [Fact]
    public async Task Import_ParsesDeposits()
    {
        const string csv = "Date(UTC);Coin;Network;Amount;Address;Kommentar\n" +
            "23.01.2018 20:25;ETH;ETH;0,400000;0x55a7e...c393;von bitcoin.de";

        using var context = TestHelper.CreateContext();
        var importer = new OkxDepositImporter(context);
        await importer.Import(new ImportArgs { Wallet = Wallet }, () => TestHelper.CreateStream(csv));

        context.CryptoTransactions.Should().HaveCount(1);
        var tx = context.CryptoTransactions.First();
        tx.TransactionType.Should().Be(TransactionType.Receive);
        tx.Symbol.Should().Be("ETH");
        tx.Quantity.Should().Be(0.400000m);
        tx.Network.Should().Be("ETH");
        ((IFlow)tx).FlowDirection.Should().Be(FlowDirection.Inflow);
    }
}
