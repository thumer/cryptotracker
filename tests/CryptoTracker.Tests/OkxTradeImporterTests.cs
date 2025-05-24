using CryptoTracker.Entities;
using CryptoTracker.Import;
using FluentAssertions;

namespace CryptoTracker.Tests;

public class OkxTradeImporterTests
{
    private const string Wallet = "Main";

    [Fact]
    public async Task Import_ParsesTrades()
    {
        const string csv = "Date(UTC);Pair;Side;Price;Executed;Amount\n" +
            "2018-01-23 20:17:13;IOTAUSDT;BUY;2,37;1000,0;2370,00USDT";

        using var context = TestHelper.CreateContext();
        var importer = new OkxTradeImporter(context);
        await importer.Import(new ImportArgs { Wallet = Wallet }, () => TestHelper.CreateStream(csv));

        context.CryptoTrades.Should().HaveCount(2);
        var sell = context.CryptoTrades.First(t => t.TradeType == TradeType.Sell);
        var buy = context.CryptoTrades.First(t => t.TradeType == TradeType.Buy);

        sell.Symbol.Should().Be("IOTA");
        sell.OpositeSymbol.Should().Be("USDT");
        sell.Quantity.Should().Be(1000m);
        sell.Price.Should().BeApproximately(2.37m, 0.00001m);
        buy.Symbol.Should().Be("USDT");
        buy.Quantity.Should().Be(2370m);
        buy.Price.Should().BeApproximately(1m / sell.Price, 0.00001m);
        sell.OppositeTradeId.Should().Be(buy.Id);
        buy.OppositeTradeId.Should().Be(sell.Id);
    }
}
