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

public class BitpandaTransactionImporterTests : DbTestBase
{
    private const string WalletName = "TestWallet";
    private const string Csv = "Transaction ID,Date,Buy/Sell,Asset,Fiat Amount,Fiat Currency,Crypto Amount,Crypto Currency,Fee,Fee asset,Spread,Spread Currency,Tax Fiat,Address,Comment\n" +
        "c1d2f3e4-...,2022-01-03T12:56:40+01:00,Buy,ADA,100,EUR,300,ADA,1,ADA,0,EUR,0,,bitpanda.com\n" +
        "b9e8d7c6-...,2022-01-29T16:21:30+01:00,Sell,ETH,50,EUR,0.012,ETH,0.5,ETH,0,EUR,0,,bitpanda.com\n" +
        "a4b3c2d1-...,2023-03-15T18:45:00+01:00,Buy,BTC,250,EUR,0.005,BTC,1,BTC,0,EUR,0,,promo\n" +
        "d8c7b6a5-...,2024-07-21T09:05:33+02:00,Convert,SOL,0,EUR,1.5,SOL,0,SOL,0,EUR,0,,staking-reward\n";

    [Fact]
    public async Task ImportCreatesBitpandaTradePair()
    {
        var importer = new BitpandaTransactionImporter(DbContext);

        var wallet = new Wallet { Name = WalletName };
        DbContext.Wallets.Add(wallet);
        DbContext.SaveChanges();

        await importer.Import(new ImportArgs { Wallet = wallet }, () => new MemoryStream(Encoding.UTF8.GetBytes(Csv)));

        DbContext.CryptoTrades.Should().HaveCount(6);
        DbContext.CryptoTrades.Should().Contain(t => t.Symbol == "EUR" && t.OpositeSymbol == "ADA");
    }
}
