using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryptoTracker;
using CryptoTracker.Entities;
using CryptoTracker.Import;
using CryptoTracker.Shared;
using FluentAssertions;
using Xunit;

namespace CryptoTracker.Tests.Importers;

public class LedgerTransactionImporterTests : DbTestBase
{
    private const string WalletName = "LedgerWallet";
    private const string Csv = "Datum (UTC);Typ;Coin;Network;Address;Amount;TransactionFee;Kommentar\n" +
                               "01.01.2024 12:00;Eingang;BTC;BTC;bc1abc;0,10000000;0;von wallet\n" +
                               "01.01.2024 13:00;Ausgang;ETH;ETH;0xabc;1,50000000;0,00500000;an wallet\n";

    [Fact]
    public async Task ImportCreatesLedgerTransactions()
    {
        var importer = new LedgerTransactionImporter(DbContext);

        var wallet = new Wallet { Name = WalletName };
        DbContext.Wallets.Add(wallet);
        DbContext.SaveChanges();

        await importer.Import(new ImportArgs { Wallet = wallet }, () => new MemoryStream(Encoding.UTF8.GetBytes(Csv)));

        DbContext.CryptoTransactions.Should().HaveCount(2);
        var receive = DbContext.CryptoTransactions.First(t => t.TransactionType == TransactionType.Receive);
        receive.Symbol.Should().Be("BTC");
        receive.Address.Should().Be("bc1abc");
        receive.Quantity.Should().Be(0.10000000m);
        receive.Fee.Should().Be(0m);

        var send = DbContext.CryptoTransactions.First(t => t.TransactionType == TransactionType.Send);
        send.Symbol.Should().Be("ETH");
        send.Address.Should().Be("0xabc");
        send.Quantity.Should().Be(1.50000000m + 0.00500000m);
        send.Fee.Should().Be(0.00500000m);
    }
}
