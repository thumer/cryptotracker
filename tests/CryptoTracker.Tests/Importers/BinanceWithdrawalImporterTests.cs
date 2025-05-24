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

public class BinanceWithdrawalImporterTests : DbTestBase
{
    private const string Wallet = "TestWallet";
    private const string Csv = "Date(UTC);Coin;Network;Amount;Fee;Address;TXID;Status;Comment\n" +
        "02.02.2018 09:14;BTC;BTC;0,050;0,0005;3J98t1WpEZ73...;c47dd1...2ef9;Completed;an eigene Wallet\n" +
        "15.06.2019 12:30;ETH;ERC20;2,000;0,005;0x8e12fa...;4bd913...a1b2;Completed;Hardware-Wallet\n" +
        "23.08.2021 18:20;BNB;BEP20;1,000;0,0001;bnb14k55...;baf3c5...c4d5;Processing;Withdrawal Test\n";

    [Fact]
    public async Task ImportCreatesWithdrawal()
    {
        var importer = new BinanceWithdrawalImporter(DbContext);

        await importer.Import(new ImportArgs { Wallet = Wallet }, () => new MemoryStream(Encoding.UTF8.GetBytes(Csv)));

        DbContext.CryptoTransactions.Should().HaveCount(3);
        var tx = DbContext.CryptoTransactions.First();

        tx.TransactionType.Should().Be(TransactionType.Send);
        tx.Wallet.Should().Be(Wallet);
        tx.Symbol.Should().Be("BTC");
        tx.DateTime.Should().Be(new DateTime(2018, 2, 2, 9, 14, 0));
        tx.Quantity.Should().Be(0.0505m);
        tx.Fee.Should().Be(0.0005m);
        tx.QuantityAfterFee.Should().Be(0.050m);
        ((IFlow)tx).FlowDirection.Should().Be(FlowDirection.Outflow);
        ((IFlow)tx).FlowAmount.Should().Be(0.0505m);
    }
}
