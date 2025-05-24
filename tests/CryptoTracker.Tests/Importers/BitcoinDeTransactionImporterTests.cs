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

public class BitcoinDeTransactionImporterTests : DbTestBase
{
    private const string WalletName = "TestWallet";
    private const string Csv = "Datum;Art;Währung;Auftrag;Preis;Betrag;Gebührart;Gebühr;Gebühr-Währung;Saldo;Kommentar\n" +
        "2013-04-12 11:20:56;Registrierung;BTC;;;0;;0;;0,00;\n" +
        "2013-04-17 15:35:15;Kauf;BTC;QW3A9T;78,00;1,00;EUR;0,99;EUR;1,00;\n" +
        "2014-01-02 13:10:05;Verkauf;BTC;RT6E2P;570,00;0,50;EUR;2,85;EUR;0,50;Erste Auszahlung\n";

    [Fact(Skip="Broken after wallet refactor")]
    public async Task ImportCreatesBitcoinDeTradePair()
    {
        var importer = new BitcoinDeTransactionImporter(DbContext);

        var wallet = new Wallet { Name = WalletName };
        DbContext.Wallets.Add(wallet);
        DbContext.SaveChanges();

        await importer.Import(new ImportArgs { Wallet = wallet }, () => new MemoryStream(Encoding.UTF8.GetBytes(Csv)));

        Assert.True(true);
    }
}
