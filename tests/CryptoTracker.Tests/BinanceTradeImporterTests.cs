using CryptoTracker.Entities;
using CryptoTracker.Import;
using CryptoTracker.Import.Objects;
using FluentAssertions;

namespace CryptoTracker.Tests;

public class BinanceTradeImporterTests
{
    private const string Wallet = "Main";

    [Fact]
    public async Task Import_ParsesTradesAndLinksPairs()
    {
        const string csv = "Date(UTC),Pair,Side,Price,Executed,Amount,Fee\n" +
            "2023-11-11 14:02:15,LTCETH,BUY,0.0508376,1.63850661LTC,0.06ETH,0.000LTC\n" +
            "2024-03-17 18:29:02,LTCUSDT,SELL,70.60922383,0.56LTC,48.216USDT,0.048216USDT\n" +
            "2018-01-23 20:17:13,ADAETH,BUY,0.00069169,148.0000000000ADA,0.08496976ETH,0.0031142300BNB";

        using var context = TestHelper.CreateContext();
        var importer = new BinanceTradeImporter(context);
        await importer.Import(new ImportArgs { Wallet = Wallet }, () => TestHelper.CreateStream(csv));

        context.CryptoTrades.Should().HaveCount(6);

        var firstSell = context.CryptoTrades.First(t => t.TradeType == TradeType.Sell);
        var firstBuy = context.CryptoTrades.First(t => t.TradeType == TradeType.Buy && t.DateTime == firstSell.DateTime);

        firstSell.Symbol.Should().Be("ETH");
        firstSell.OpositeSymbol.Should().Be("LTC");
        firstSell.Wallet.Should().Be(Wallet);
        firstSell.Price.Should().BeApproximately(1m / 0.0508376m, 0.0000001m);
        firstSell.Quantity.Should().Be(0.06m);
        firstSell.QuantityAfterFee.Should().Be(0.06m);
        ((IFlow)firstSell).FlowDirection.Should().Be(FlowDirection.Outflow);
        ((IFlow)firstSell).FlowAmount.Should().Be(0.06m);

        firstBuy.Symbol.Should().Be("LTC");
        firstBuy.OpositeSymbol.Should().Be("ETH");
        firstBuy.Price.Should().Be(0.0508376m);
        firstBuy.Quantity.Should().Be(1.63850661m);
        firstBuy.QuantityAfterFee.Should().Be(1.63850661m);
        ((IFlow)firstBuy).FlowDirection.Should().Be(FlowDirection.Inflow);
        ((IFlow)firstBuy).FlowAmount.Should().Be(1.63850661m);

        firstSell.OppositeTradeId.Should().Be(firstBuy.Id);
        firstBuy.OppositeTradeId.Should().Be(firstSell.Id);
    }
}
