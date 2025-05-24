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
    private const string Csv = @"""Transaction ID"",Timestamp,""Transaction Type"",In/Out,""Amount Fiat"",Fiat,""Amount Asset"",Asset,""Asset market price"",""Asset market price currency"",""Asset class"",""Product ID"",Fee,""Fee asset"",Spread,""Spread Currency"",""Tax Fiat"",""Address"",""Comment""
Febb6de9a-fe34-45b4-9d4b-79e6b08fa29d,2022-01-03T12:56:40+01:00,deposit,incoming,200.00,EUR,-,EUR,-,-,Fiat,-,0.00000000,EUR,-,-,0.00,,
Te99fd4b9-97d2-4665-80f3-db45e6dfceab,2022-01-29T16:21:30+01:00,buy,outgoing,200.00,EUR,0.04245838,ETH,2355.25,EUR,Cryptocurrency,5,-,-,-,-,0.00,,
C1ca8a52b-3672-4ced-954f-ad2929be66f9,2022-03-21T18:56:17+01:00,withdrawal,outgoing,0,EUR,0.48188785,ETH,0.00,-,Cryptocurrency,5,0.00297774,ETH,-,-,-,0xebe19316f151a4cf393e46371020e5079d7f6d91,abc
73ba57e0-0c32-45cc-aeac-60393f718a6b,2022-09-21T19:59:41+02:00,transfer,incoming,1.15,EUR,0.38122557,ETHW,5.63,EUR,Cryptocurrency,2115,-,-,-,-,-,,ebc";

    [Fact]
    public async Task ImportCreatesBitpandaTradePair()
    {
        var importer = new BitpandaTransactionImporter(DbContext);

        var wallet = new Wallet { Name = WalletName };
        DbContext.Wallets.Add(wallet);
        DbContext.SaveChanges();

        await importer.Import(new ImportArgs { Wallet = wallet }, () => new MemoryStream(Encoding.UTF8.GetBytes(Csv)));

        DbContext.CryptoTrades.Should().HaveCount(2);
        DbContext.CryptoTrades.Should().Contain(t => t.Symbol == "EUR" && t.OpositeSymbol == "ETH");
    }
}
