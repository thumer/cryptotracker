using CryptoTracker.Entities;
using CryptoTracker.Import;
using FluentAssertions;

namespace CryptoTracker.Tests;

public class BitpandaTransactionImporterTests
{
    private const string Wallet = "Main";

    [Fact]
    public async Task Import_ParsesBuySellAndLinksTrades()
    {
        const string csv = "Transaction ID,Date,Buy/Sell,Asset,Fiat Amount,Fiat Currency,Crypto Amount,Crypto Currency,Fee,Fee asset,Spread,Spread Currency,Tax Fiat,Address,Comment\n" +
            "c1d2f3e4-...,2022-01-03T12:56:40+01:00,Buy,ADA,100,EUR,300,ADA,1,ADA,0,EUR,0,,bitpanda.com";

        using var context = TestHelper.CreateContext();
        var importer = new BitpandaTransactionImporter(context);
        await importer.Import(new ImportArgs { Wallet = Wallet }, () => TestHelper.CreateStream(csv));

        context.CryptoTrades.Should().HaveCount(2);
        var sell = context.CryptoTrades.First(t => t.TradeType == TradeType.Sell);
        var buy = context.CryptoTrades.First(t => t.TradeType == TradeType.Buy);
        sell.Symbol.Should().Be("EUR");
        sell.OpositeSymbol.Should().Be("ADA");
        buy.Symbol.Should().Be("ADA");
        buy.Quantity.Should().Be(300m);
        sell.OppositeTradeId.Should().Be(buy.Id);
        buy.OppositeTradeId.Should().Be(sell.Id);
    }
}
