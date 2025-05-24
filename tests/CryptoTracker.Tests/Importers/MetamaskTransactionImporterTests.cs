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

public class MetamaskTransactionImporterTests : DbTestBase
{
    private const string Wallet = "TestWallet";
    private const string Csv = "Datum;Typ;Coin;Network;Amount;TransactionFee;Kommentar\n" +
        "01.10.2021 12:24;Eingang;ETH;BSC;0,179936;0,000064;von binance.com\n" +
        "02.07.2021 20:38;Eingang;ETH;BSC;0,209932;0,000068;von binance.com\n" +
        "02.07.2021 21:12;Eingang;BNB;BSC;0,0795;0,0005;von binance.com\n" +
        "10.11.2021 21:10;Ausgang;ETH;BSC;0,35658378;0;an binance.com\n" +
        "12.12.2022 08:08;Ausgang;USDT;BSC;250;0,8;an CEX.io\n";

    [Fact]
    public async Task ImportCreatesMetamaskTransaction()
    {
        var importer = new MetamaskTransactionImporter(DbContext);

        await importer.Import(new ImportArgs { Wallet = Wallet }, () => new MemoryStream(Encoding.UTF8.GetBytes(Csv)));

        DbContext.CryptoTransactions.Should().HaveCount(5);
        var tx = DbContext.CryptoTransactions.First();

        tx.TransactionType.Should().Be(TransactionType.Receive);
        tx.Symbol.Should().Be("ETH");
        tx.Quantity.Should().Be(0.179936m + 0.000064m);
        tx.Fee.Should().Be(0.000064m);
        tx.QuantityAfterFee.Should().Be(0.179936m);
        ((IFlow)tx).FlowDirection.Should().Be(FlowDirection.Inflow);
        ((IFlow)tx).FlowAmount.Should().Be(0.179936m);
    }
}
