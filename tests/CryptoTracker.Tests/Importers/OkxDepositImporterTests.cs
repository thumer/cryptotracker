using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryptoTracker;
using CryptoTracker.Import;
using CryptoTracker.Entities;
using FluentAssertions;
using Xunit;

namespace CryptoTracker.Tests.Importers;

public class OkxDepositImporterTests : DbTestBase
{
    private const string Wallet = "TestWallet";
    private const string Csv = "Date(UTC);Coin;Network;Amount;Address;Comment\n" +
        "23.01.2018 20:25;ETH;ETH;0,400000;0x55a7e...c393;von bitcoin.de\n" +
        "01.02.2018 19:46;BTC;BTC;0,026000;bc1qxy2...8kg0;von binance.com\n" +
        "17.07.2022 07:13;ADA;ADA;950,00;DdzFFzCq...;interner Transfer\n";

    [Fact]
    public async Task ImportCreatesOkxDeposit()
    {
        var importer = new OkxDepositImporter(DbContext);

        await importer.Import(new ImportArgs { Wallet = Wallet }, () => new MemoryStream(Encoding.UTF8.GetBytes(Csv)));

        DbContext.CryptoTransactions.Should().HaveCount(3);
        var tx = DbContext.CryptoTransactions.First();
        tx.TransactionType.Should().Be(TransactionType.Receive);
        tx.Symbol.Should().Be("ETH");
        tx.Quantity.Should().Be(0.400000m);
        tx.Fee.Should().Be(0m);
        tx.QuantityAfterFee.Should().Be(0.400000m);
        ((IFlow)tx).FlowDirection.Should().Be(FlowDirection.Inflow);
        ((IFlow)tx).FlowAmount.Should().Be(0.400000m);
    }
}
