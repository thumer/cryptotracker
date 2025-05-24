using CryptoTracker.Entities;
using CryptoTracker.Import;
using FluentAssertions;

namespace CryptoTracker.Tests;

public class MetamaskTradeImporterTests
{
    private const string Wallet = "Main";

    [Fact]
    public async Task Import_ParsesTrades()
    {
        const string csv = "Date(UTC);Pair;Side;Price;Executed;Amount;Fee;Tradingplatform\n" +
            "02.07.2021;BabyDo-ETH;BUY;1,0142E-12 ETH;1,8E+11 BabyDo;0,17 ETH;2E+10 BabyDo;pancakeswap.finance";

        using var context = TestHelper.CreateContext();
        var importer = new MetamaskTradeImporter(context);
        await importer.Import(new ImportArgs { Wallet = Wallet }, () => TestHelper.CreateStream(csv));

        context.CryptoTrades.Should().HaveCount(2);
        var sell = context.CryptoTrades.First(t => t.TradeType == TradeType.Sell);
        var buy = context.CryptoTrades.First(t => t.TradeType == TradeType.Buy);
        sell.Symbol.Should().Be("BabyDo");
        buy.Symbol.Should().Be("ETH");
        sell.OppositeTradeId.Should().Be(buy.Id);
        buy.OppositeTradeId.Should().Be(sell.Id);
    }
}
