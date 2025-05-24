using CryptoTracker.Entities;
using CryptoTracker.Import;
using FluentAssertions;

namespace CryptoTracker.Tests;

public class MetamaskTransactionImporterTests
{
    private const string Wallet = "Main";

    [Fact]
    public async Task Import_ParsesTransactions()
    {
        const string csv = "Datum;Typ;Coin;Network;Amount;TransactionFee;Kommentar\n" +
            "01.10.2021 12:24;Eingang;ETH;BSC;0,179936;0,000064;von binance.com";

        using var context = TestHelper.CreateContext();
        var importer = new MetamaskTransactionImporter(context);
        await importer.Import(new ImportArgs { Wallet = Wallet }, () => TestHelper.CreateStream(csv));

        context.CryptoTransactions.Should().HaveCount(1);
        var tx = context.CryptoTransactions.First();
        tx.TransactionType.Should().Be(TransactionType.Receive);
        tx.Symbol.Should().Be("ETH");
        tx.Quantity.Should().Be(0.179936m);
        tx.Fee.Should().Be(0.000064m);
        tx.QuantityAfterFee.Should().Be(0.179872m);
        ((IFlow)tx).FlowDirection.Should().Be(FlowDirection.Inflow);
        ((IFlow)tx).FlowAmount.Should().Be(tx.QuantityAfterFee);
    }
}
