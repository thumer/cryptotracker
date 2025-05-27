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

public class OkxTradeImporterTests : DbTestBase
{
    private const string WalletName = "TestWallet";
    private const string Csv = "Date(UTC);Pair;Side;Price;Executed;Amount\n" +
        "2018-01-23 20:17:13;IOTAUSDT;BUY;2,37;1000,0;2370,00USDT\n" +
        "2022-03-10 12:04:59;ETHUSDT;SELL;2750,75;0,50;1375,38USDT\n" +
        "2023-05-05 11:11:11;SOLUSDT;BUY;23,45;150,0;3517,50USDT\n";

    [Fact]
    public async Task ImportCreatesOkxTradePair()
    {
        var importer = new OkxTradeImporter(DbContext);

        var wallet = new Wallet { Name = WalletName };
        DbContext.Wallets.Add(wallet);
        DbContext.SaveChanges();

        await importer.Import(new ImportArgs { Wallet = wallet }, () => new MemoryStream(Encoding.UTF8.GetBytes(Csv)));

        DbContext.CryptoTrades.Should().HaveCount(6);
        var date = new DateTimeOffset(2018, 1, 23, 20, 17, 13, TimeSpan.Zero);
        var sell = DbContext.CryptoTrades.Single(t => t.DateTime == date && t.TradeType == TradeType.Sell);
        var buy = DbContext.CryptoTrades.Single(t => t.DateTime == date && t.TradeType == TradeType.Buy);

        sell.Symbol.Should().Be("USDT");
        sell.OppositeSymbol.Should().Be("IOTA");
        sell.Price.Should().Be(1m / 2.37m);
        sell.Quantity.Should().Be(2370m);

        buy.Symbol.Should().Be("IOTA");
        buy.OppositeSymbol.Should().Be("USDT");
        buy.Price.Should().Be(2.37m);
        buy.Quantity.Should().Be(1000m);

        sell.OppositeTradeId.Should().Be(buy.Id);
        buy.OppositeTradeId.Should().Be(sell.Id);
    }
}
