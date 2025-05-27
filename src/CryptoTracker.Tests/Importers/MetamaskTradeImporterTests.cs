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

public class MetamaskTradeImporterTests : DbTestBase
{
    private const string WalletName = "TestWallet";
    private const string Csv = "Date(UTC);Pair;Side;Price;Executed;Amount;Fee;Tradingplatform\n" +
        "02.07.2021;BabyDo-ETH;BUY;1,0142E-12 ETH;1,8E+11 BabyDo;0,17 ETH;2E+10 BabyDo;pancakeswap.finance\n" +
        "01.10.2021;WDUCX-ETH;BUY;9,61538E-05 ETH;1456,161 WDUCX;0,14 ETH;0 WDUCX;pancakeswap.finance\n" +
        "05.12.2021;ABC-BNB;SELL;0,00001234 BNB;100000 ABC;0,01 BNB;1000 ABC;pancakeswap.finance\n";

    [Fact]
    public async Task ImportCreatesMetamaskTrades()
    {
        var importer = new MetamaskTradeImporter(DbContext);

        var wallet = new Wallet { Name = WalletName };
        DbContext.Wallets.Add(wallet);
        DbContext.SaveChanges();

        await importer.Import(new ImportArgs { Wallet = wallet }, () => new MemoryStream(Encoding.UTF8.GetBytes(Csv)));

        DbContext.CryptoTrades.Should().HaveCount(6);
        var date = new DateTimeOffset(2021, 7, 2, 0, 0, 0, TimeSpan.Zero);
        var sell = DbContext.CryptoTrades.Single(t => t.DateTime == date && t.TradeType == TradeType.Sell);
        var buy = DbContext.CryptoTrades.Single(t => t.DateTime == date && t.TradeType == TradeType.Buy);

        sell.Symbol.Should().Be("ETH");
        sell.OppositeSymbol.Should().Be("BabyDo");
        buy.Symbol.Should().Be("BabyDo");
        buy.OppositeSymbol.Should().Be("ETH");
        sell.OppositeTradeId.Should().Be(buy.Id);
        buy.OppositeTradeId.Should().Be(sell.Id);
    }
}
