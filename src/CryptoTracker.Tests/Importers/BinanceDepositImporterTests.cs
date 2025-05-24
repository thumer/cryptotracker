using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryptoTracker;
using CryptoTracker.Import;
using CryptoTracker.Entities;
using CryptoTracker.Shared;
using FluentAssertions;
using Xunit;

namespace CryptoTracker.Tests.Importers;

public class BinanceDepositImporterTests : DbTestBase
{
    private const string WalletName = "TestWallet";
    private const string Csv = "Date(UTC);Coin;Network;Amount;TransactionFee;Address;TXID;Comment\n" +
        "29.12.2017 21:10;BTC;BTC;0,57127657;0;1BvBMrv6gnwf...;a3f1c7...4d1391;von bitcoin.de\n" +
        "31.12.2017 13:30;BTC;BTC;0,01907041;0;1BvBMrv6gnwf...;f7d12e...29ea1;von bitcoin.de\n" +
        "13.03.2018 10:12;ETH;ETH;1,00000000;0;0x9c4e76...3b8b9a;dd61a7...462c2f;von okx.com\n" +
        "21.04.2019 17:55;ADA;ADA;532,75;0;DdzFFzCq...;09ff3b...c2699c;via wallet\n" +
        "10.05.2020 08:03;USDT;TRC20;250,00;0;TEvSFk...hy1p;5e7b2f...ab4def;Airdrop\n";

    [Fact]
    public async Task ImportCreatesDeposit()
    {
        var importer = new BinanceDepositImporter(DbContext);

        var wallet = new Wallet { Name = WalletName };
        DbContext.Wallets.Add(wallet);
        DbContext.SaveChanges();

        await importer.Import(new ImportArgs { Wallet = wallet }, () => new MemoryStream(Encoding.UTF8.GetBytes(Csv)));

        DbContext.CryptoTransactions.Should().HaveCount(5);
        var tx = DbContext.CryptoTransactions.First();

        tx.Wallet.Name.Should().Be(WalletName);
        tx.TransactionType.Should().Be(TransactionType.Receive);
        tx.Symbol.Should().Be("BTC");
        tx.DateTime.Should().Be(new DateTimeOffset(2017, 12, 29, 21, 10, 0, TimeSpan.Zero));
        tx.Quantity.Should().Be(0.57127657m);
        tx.Fee.Should().Be(0m);
        tx.QuantityAfterFee.Should().Be(0.57127657m);
        ((IFlow)tx).FlowDirection.Should().Be(FlowDirection.Inflow);
        ((IFlow)tx).FlowAmount.Should().Be(0.57127657m);
    }
}
