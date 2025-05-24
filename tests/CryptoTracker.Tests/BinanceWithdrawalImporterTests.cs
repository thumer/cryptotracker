using CryptoTracker.Entities;
using CryptoTracker.Import;
using FluentAssertions;

namespace CryptoTracker.Tests;

public class BinanceWithdrawalImporterTests
{
    private const string Wallet = "Main";

    [Fact]
    public async Task Import_ParsesWithdrawals()
    {
        const string csv = "Date(UTC);Coin;Network;Amount;TransactionFee;Address;TXID;Comment\n" +
            "02.02.2018 09:14;BTC;BTC;0,050;0,0005;3J98t1WpEZ73...;c47dd1...2ef9;an eigene Wallet";

        using var context = TestHelper.CreateContext();
        var importer = new BinanceWithdrawalImporter(context);
        await importer.Import(new ImportArgs { Wallet = Wallet }, () => TestHelper.CreateStream(csv));

        context.CryptoTransactions.Should().HaveCount(1);
        var tx = context.CryptoTransactions.First();
        tx.TransactionType.Should().Be(TransactionType.Send);
        tx.Symbol.Should().Be("BTC");
        tx.Quantity.Should().Be(0.050m + 0.0005m);
        tx.Fee.Should().Be(0.0005m);
        tx.QuantityAfterFee.Should().Be(0.050m);
        ((IFlow)tx).FlowDirection.Should().Be(FlowDirection.Outflow);
        ((IFlow)tx).FlowAmount.Should().Be(0.0505m); // outflow uses Quantity
        tx.OppositeTransactionId.Should().BeNull();
    }
}
