using CryptoTracker.Entities;
using CryptoTracker.Import;
using FluentAssertions;

namespace CryptoTracker.Tests;

public class BitcoinDeTransactionImporterTests
{
    private const string Wallet = "Main";

    [Fact]
    public async Task Import_ParsesTradesAndTransactions()
    {
        const string csv = "Datum;Typ;Währung;Referenz;Adresse;Kurs;\"Einheit (Kurs)\";\"Crypto vor Gebühr\";\"Menge vor Gebühr\";\"Einheit (Menge vor Gebühr)\";\"Crypto nach Bitcoin.de-Gebühr\";\"Menge nach Bitcoin.de-Gebühr\";\"Einheit (Menge nach Bitcoin.de-Gebühr)\";\"Zu- / Abgang\";Kontostand;Kommentar\n" +
            "2013-04-17 15:35:15;Kauf;BTC;QW3A9T;;61.00;BTC / EUR;1.00000000;61.00;EUR;0.99000000;60.69;EUR;0.99000000;0.99000000;\n";

        using var context = TestHelper.CreateContext();
        var importer = new BitcoinDeTransactionImporter(context);
        await importer.Import(new ImportArgs { Wallet = Wallet }, () => TestHelper.CreateStream(csv));

        context.CryptoTrades.Should().HaveCount(2);
        var sell = context.CryptoTrades.First(t => t.TradeType == TradeType.Sell);
        var buy = context.CryptoTrades.First(t => t.TradeType == TradeType.Buy);
        sell.Symbol.Should().Be("EUR");
        sell.OpositeSymbol.Should().Be("BTC");
        sell.Price.Should().BeApproximately(61m, 0.00001m);
        buy.Symbol.Should().Be("BTC");
        buy.Quantity.Should().Be(1.00000000m);
        sell.OppositeTradeId.Should().Be(buy.Id);
        buy.OppositeTradeId.Should().Be(sell.Id);
    }
}
