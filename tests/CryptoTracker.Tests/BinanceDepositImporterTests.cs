using CryptoTracker.Entities;
using CryptoTracker.Import;
using FluentAssertions;

namespace CryptoTracker.Tests;

public class BinanceDepositImporterTests
{
    private const string Wallet = "Main";

    [Fact]
    public async Task Import_ParsesDeposits()
    {
        const string csv = "Date(UTC);Coin;Network;Amount;TransactionFee;Address;TXID;Comment\n" +
            "29.12.2017 21:10;BTC;BTC;0,57127657;0;1BvBMrv6gnwf...;a3f1c7...4d1391;von bitcoin.de\n" +
            "31.12.2017 13:30;BTC;BTC;0,01907041;0;1BvBMrv6gnwf...;f7d12e...29ea1;von bitcoin.de";

        using var context = TestHelper.CreateContext();
        var importer = new BinanceDepositImporter(context);
        await importer.Import(new ImportArgs { Wallet = Wallet }, () => TestHelper.CreateStream(csv));

        context.CryptoTransactions.Should().HaveCount(2);
        var tx = context.CryptoTransactions.First();
        tx.Wallet.Should().Be(Wallet);
        tx.TransactionType.Should().Be(TransactionType.Receive);
        tx.Symbol.Should().Be("BTC");
        tx.Quantity.Should().Be(0.57127657m);
        tx.QuantityAfterFee.Should().Be(0.57127657m);
        tx.Address.Should().StartWith("1BvBMrv6");
        ((IFlow)tx).FlowDirection.Should().Be(FlowDirection.Inflow);
        ((IFlow)tx).FlowAmount.Should().Be(tx.QuantityAfterFee);
        tx.OppositeTransactionId.Should().BeNull();
    }
}
