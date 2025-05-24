using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryptoTracker;
using CryptoTracker.Import;
using CryptoTracker.Import.Objects;
using CryptoTracker.Entities;
using CryptoTracker.Shared;
using FluentAssertions;
using Xunit;

namespace CryptoTracker.Tests.Importers;

public class BinanceTradeImporterTests : DbTestBase
{
    private const string WalletName = "TestWallet";

    private const string Csv = "Date(UTC),Pair,Side,Price,Executed,Amount,Fee\n" +
        "2023-11-11 14:02:15,LTCETH,BUY,0.0508376,1.63850661LTC,0.06ETH,0.000LTC\n" +
        "2024-03-17 18:29:02,LTCUSDT,SELL,70.60922383,0.56LTC,48.216USDT,0.048216USDT\n" +
        "2018-01-23 20:17:13,ADAETH,BUY,0.00069169,148.0000000000ADA,0.08496976ETH,0.0031142300BNB\n";

    [Fact]
    public async Task ImportCreatesTradesWithOppositeLinks()
    {
        var importer = new BinanceTradeImporter(DbContext);

        var wallet = new Wallet { Name = WalletName };
        DbContext.Wallets.Add(wallet);
        DbContext.SaveChanges();

        await importer.Import(new ImportArgs { Wallet = wallet }, () => new MemoryStream(Encoding.UTF8.GetBytes(Csv)));

        DbContext.CryptoTrades.Should().HaveCount(6);

        var date = new DateTimeOffset(2023, 11, 11, 14, 2, 15, TimeSpan.Zero);
        var sell = DbContext.CryptoTrades.Single(t => t.DateTime == date && t.TradeType == TradeType.Sell);
        var buy = DbContext.CryptoTrades.Single(t => t.DateTime == date && t.TradeType == TradeType.Buy);

        sell.Wallet.Name.Should().Be(WalletName);
        sell.Symbol.Should().Be("ETH");
        sell.OpositeSymbol.Should().Be("LTC");
        sell.Price.Should().Be(1m / 0.0508376m);
        sell.Quantity.Should().Be(0.06m);
        sell.Fee.Should().Be(0m);
        sell.QuantityAfterFee.Should().Be(0.06m);
        ((IFlow)sell).FlowDirection.Should().Be(FlowDirection.Outflow);
        ((IFlow)sell).FlowAmount.Should().Be(0.06m);

        buy.Wallet.Name.Should().Be(WalletName);
        buy.Symbol.Should().Be("LTC");
        buy.OpositeSymbol.Should().Be("ETH");
        buy.Price.Should().Be(0.0508376m);
        buy.Quantity.Should().Be(1.63850661m);
        buy.Fee.Should().Be(0m);
        buy.QuantityAfterFee.Should().Be(1.63850661m);
        ((IFlow)buy).FlowDirection.Should().Be(FlowDirection.Inflow);
        ((IFlow)buy).FlowAmount.Should().Be(1.63850661m);

        sell.OppositeTradeId.Should().Be(buy.Id);
        buy.OppositeTradeId.Should().Be(sell.Id);
    }
}
