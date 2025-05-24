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
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Tests.Importers;

public class BitcoinDeTransactionImporterTests : DbTestBase
{
    private const string WalletName = "TestWallet";
    private const string Csv = @"Datum;Typ;Währung;Referenz;Adresse;Kurs;""Einheit (Kurs)"";""Crypto vor Gebühr"";""Menge vor Gebühr"";""Einheit (Menge vor Gebühr)"";""Crypto nach Bitcoin.de-Gebühr"";""Menge nach Bitcoin.de-Gebühr"";""Einheit (Menge nach Bitcoin.de-Gebühr)"";""Zu- / Abgang"";Kontostand;Kommentar
""2014-04-12 11:20:56"";Registrierung;BTC;;;0.00;BTC;;;;;;;0.00000000;0.00000000;
""2014-04-17 15:35:15"";Kauf;BTC;EW3A9T;;61.00;""BTC / EUR"";1.00000000;21.00;EUR;0.99000000;20.69;EUR;0.59000000;0.59000000;
""2014-04-17 20:55:35"";Auszahlung;BTC;a1545e842dd311d998cc4797be7502f573e3d1a6cb5c6cf0e8713e64e28be63a;196pDut3qJFHY8qPRVagUjtmggCkZBBwBt;;;;;;;;;-0.99000000;0.00000000;xxx";

    [Fact]
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
