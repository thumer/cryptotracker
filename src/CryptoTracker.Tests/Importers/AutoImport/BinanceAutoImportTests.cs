using System.Text;
using CryptoTracker.Entities;
using CryptoTracker.Services;
using FluentAssertions;

namespace CryptoTracker.Tests.Importers.AutoImport;

public class BinanceAutoImportTests : DbTestBase
{
    private const string WalletName = "BinanceWallet";

    [Fact]
    public async Task ImportDepositCsv()
    {
        var csv = BuildBinanceDepositCsv();
        await ImportAsync("binance_deposit_history.csv", csv);

        DbContext.Wallets.Should().ContainSingle(w => w.Name == WalletName);
        DbContext.CryptoTransactions.Should().HaveCount(10);
        DbContext.CryptoTransactions.Should().OnlyContain(t => t.TransactionType == TransactionType.Receive);
    }

    [Fact]
    public async Task ImportWithdrawalCsv()
    {
        var csv = BuildBinanceWithdrawalCsv();
        await ImportAsync("binance_withdraw_history.csv", csv);

        DbContext.CryptoTransactions.Should().HaveCount(10);
        DbContext.CryptoTransactions.Should().OnlyContain(t => t.TransactionType == TransactionType.Send);
    }

    [Fact]
    public async Task ImportTradeCsv()
    {
        var csv = BuildBinanceTradeCsv();
        await ImportAsync("binance_trading_history.csv", csv);

        DbContext.CryptoTrades.Should().HaveCount(20);
        DbContext.CryptoTrades.Should().Contain(t => t.Symbol == "ETH");
    }

    [Fact]
    public async Task ImportAccountStatementCsv()
    {
        var csv = BuildBinanceAccountStatementCsv();
        var previewService = new ImportAutoService(new DataImportService(DbContext));
        var preview = previewService.Preview(() => new MemoryStream(Encoding.UTF8.GetBytes(csv)), "binance_mixed_transactions.csv");
        preview.Success.Should().BeTrue();
        preview.TransactionRows.Should().HaveCount(10);
        preview.DocumentType.Should().BeNull();
        await ImportAsync("binance_mixed_transactions.csv", csv);

        DbContext.Wallets.Should().ContainSingle(w => w.Name == WalletName);
        DbContext.BinanceDeposits.Should().HaveCount(5);
        DbContext.BinanceWithdrawals.Should().HaveCount(5);
        DbContext.CryptoTransactions.Should().HaveCount(10);
        DbContext.CryptoTransactions.Count(t => t.TransactionType == TransactionType.Receive).Should().Be(5);
        DbContext.CryptoTransactions.Count(t => t.TransactionType == TransactionType.Send).Should().Be(5);
    }

    [Fact]
    public async Task ImportTransactionHistoryTimeCsv()
    {
        var csv = BuildBinanceTransactionHistoryTimeCsv();
        var previewService = new ImportAutoService(new DataImportService(DbContext));
        var preview = previewService.Preview(() => new MemoryStream(Encoding.UTF8.GetBytes(csv)), "transaction_history_2020-2025.csv");
        preview.Success.Should().BeTrue();
        preview.TransactionRows.Should().HaveCount(10);
        preview.DocumentType.Should().BeNull();
        await ImportAsync("transaction_history_2020-2025.csv", csv);

        DbContext.Wallets.Should().ContainSingle(w => w.Name == WalletName);
        DbContext.BinanceDeposits.Should().HaveCount(5);
        DbContext.BinanceWithdrawals.Should().HaveCount(5);
        DbContext.CryptoTransactions.Should().HaveCount(10);
        DbContext.CryptoTransactions.Count(t => t.TransactionType == TransactionType.Receive).Should().Be(5);
        DbContext.CryptoTransactions.Count(t => t.TransactionType == TransactionType.Send).Should().Be(5);
    }

    [Fact]
    public async Task ImportTransactionHistoryGroupedTradesCsv()
    {
        var csv = BuildBinanceTransactionHistoryGroupedCsv();
        await ImportAsync("transaction_history_2020-2025.csv", csv);

        DbContext.CryptoTrades.Should().HaveCount(6);
        DbContext.CryptoTransactions.Should().HaveCount(2);
        DbContext.CryptoTransactions.Count(t => t.TransactionType == TransactionType.Receive).Should().Be(1);
        DbContext.CryptoTransactions.Count(t => t.TransactionType == TransactionType.Send).Should().Be(1);
    }

    [Fact]
    public async Task ImportWithdrawHistoryTimeCsv()
    {
        var csv = BuildBinanceWithdrawHistoryTimeCsv();
        await ImportAsync("withdraw_history_2020-2025.csv", csv);

        DbContext.CryptoTransactions.Should().HaveCount(10);
        DbContext.CryptoTransactions.Should().OnlyContain(t => t.TransactionType == TransactionType.Send);
    }

    [Fact]
    public async Task ImportDepositHistoryTimeCsv()
    {
        var csv = BuildBinanceDepositHistoryTimeCsv();
        await ImportAsync("deposit_history_2020-2025.csv", csv);

        DbContext.CryptoTransactions.Should().HaveCount(10);
        DbContext.CryptoTransactions.Should().OnlyContain(t => t.TransactionType == TransactionType.Receive);
    }

    [Fact]
    public async Task ImportConvertHistoryCsv()
    {
        var csv = BuildBinanceConvertHistoryCsv();
        await ImportAsync("convert_history-2023-10-25~2024-01-23.csv", csv);

        DbContext.CryptoTrades.Should().HaveCount(20);
    }

    private async Task ImportAsync(string fileName, string csv)
    {
        var dataImportService = new DataImportService(DbContext);
        var autoService = new ImportAutoService(dataImportService);
        await autoService.ImportAsync(WalletName, () => new MemoryStream(Encoding.UTF8.GetBytes(csv)), fileName, null);
    }

    private static string BuildBinanceDepositCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date(UTC);Coin;Network;Amount;TransactionFee;Address;TXID;Comment");
        for (var i = 1; i <= 10; i++)
        {
            sb.AppendLine($"{i:00}.01.2024 10:{i:00};BTC;BTC;0,{i}0000000;0;addr{i};tx{i};Deposit {i}");
        }

        return sb.ToString();
    }

    private static string BuildBinanceWithdrawalCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date(UTC);Coin;Network;Amount;TransactionFee;Address;TXID;Comment");
        for (var i = 1; i <= 10; i++)
        {
            sb.AppendLine($"{i:00}.02.2024 11:{i:00};ETH;ERC20;0,{i}0000000;0,0001;addr{i};tx{i};Withdraw {i}");
        }

        return sb.ToString();
    }

    private static string BuildBinanceTradeCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("\"Date(UTC)\",\"Pair\",\"Side\",\"Price\",\"Executed\",\"Amount\",\"Fee\"");
        for (var i = 1; i <= 10; i++)
        {
            var side = i % 2 == 0 ? "SELL" : "BUY";
            sb.AppendLine($"\"2024-03-{i:00} 12:0{i}:00\",\"ETHUSDT\",\"{side}\",\"2300.{i}0\",\"0.01ETH\",\"23.{i}0USDT\",\"0.00001BNB\"");
        }

        return sb.ToString();
    }

    private static string BuildBinanceAccountStatementCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("\"User_ID\",\"UTC_Time\",\"Account\",\"Operation\",\"Coin\",\"Change\",\"Remark\"");
        var rows = new (string Operation, string Change)[]
        {
            ("Deposit", "0.010"),
            ("ETH 2.0 Staking Rewards", "0.004"),
            ("Staking Rewards", "0.002"),
            ("Airdrop", "0.001"),
            ("Interest", "0.003"),
            ("Withdraw", "-0.005"),
            ("Transaction Spend", "-0.006"),
            ("Transaction Fee", "-0.0002"),
            ("Fee", "-0.0001"),
            ("Withdrawal", "-0.007")
        };

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var day = i + 1;
            sb.AppendLine($"\"1\",\"2024-04-{day:00} 08:{day:00}:00\",\"Spot\",\"{row.Operation}\",\"BTC\",\"{row.Change}\",\"Auto {day}\"");
        }

        return sb.ToString();
    }

    private static string BuildBinanceTransactionHistoryTimeCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("User ID;Time;Account;Operation;Coin;Change;Remark");
        for (var i = 1; i <= 10; i++)
        {
            var op = i % 2 == 0 ? "Withdraw" : "Deposit";
            var change = i % 2 == 0 ? "-0.005" : "0.010";
            sb.AppendLine($"\"1\";\"2024-06-{i:00} 08:0{i}:00\";\"Spot\";\"{op}\";\"BTC\";\"{change}\";\"Auto {i}\"");
        }

        return sb.ToString();
    }

    private static string BuildBinanceTransactionHistoryGroupedCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("User ID;Time;Account;Operation;Coin;Change;Remark");
        sb.AppendLine("\"1\";\"2024-01-01 10:00:00\";\"Spot\";\"Transaction Spend\";\"ETH\";\"-0.5\";\"Buy group\"");
        sb.AppendLine("\"1\";\"2024-01-01 10:00:00\";\"Spot\";\"Transaction Fee\";\"BNB\";\"-0.001\";\"Buy group\"");
        sb.AppendLine("\"1\";\"2024-01-01 10:00:00\";\"Spot\";\"Transaction Buy\";\"BETH\";\"0.45\";\"Buy group\"");
        sb.AppendLine("\"1\";\"2024-01-01 10:00:00\";\"Spot\";\"Withdraw\";\"ETH\";\"-0.0001\";\"Buy group\"");

        sb.AppendLine("\"1\";\"2024-01-02 11:00:00\";\"Spot\";\"Transaction Sold\";\"BETH\";\"-1.2\";\"Sell group\"");
        sb.AppendLine("\"1\";\"2024-01-02 11:00:00\";\"Spot\";\"Transaction Revenue\";\"ETH\";\"1.0\";\"Sell group\"");
        sb.AppendLine("\"1\";\"2024-01-02 11:00:00\";\"Spot\";\"Transaction Fee\";\"BNB\";\"-0.002\";\"Sell group\"");
        sb.AppendLine("\"1\";\"2024-01-02 11:00:00\";\"Spot\";\"Withdraw\";\"ETH\";\"-0.0001\";\"Sell group\"");

        sb.AppendLine("\"1\";\"2024-01-03 12:00:00\";\"Spot\";\"Binance Convert\";\"USDT\";\"-100\";\"Convert\"");
        sb.AppendLine("\"1\";\"2024-01-03 12:00:00\";\"Spot\";\"Binance Convert\";\"BTC\";\"0.002\";\"Convert\"");

        sb.AppendLine("\"1\";\"2024-01-04 12:00:00\";\"Spot\";\"Distribution\";\"TRX\";\"10\";\"Reward\"");
        sb.AppendLine("\"1\";\"2024-01-05 12:00:00\";\"Spot\";\"Token Swap - Redenomination/Rebranding\";\"XYZ\";\"-5\";\"Swap\"");

        return sb.ToString();
    }

    private static string BuildBinanceWithdrawHistoryTimeCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Time;Coin;Network;Amount;Fee;Address;TXID;Status");
        for (var i = 1; i <= 10; i++)
        {
            sb.AppendLine($"\"2024-07-{i:00} 09:0{i}:00\";BTC;BTC;0.00{i};0.0001;addr{i};tx{i};Completed");
        }

        return sb.ToString();
    }

    private static string BuildBinanceDepositHistoryTimeCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Time;Coin;Network;Amount;Address;TXID;Status");
        for (var i = 1; i <= 10; i++)
        {
            sb.AppendLine($"\"2024-08-{i:00} 10:0{i}:00\";ETH;ERC20;0.00{i};addr{i};tx{i};Completed");
        }

        return sb.ToString();
    }

    private static string BuildBinanceConvertHistoryCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Pair,Type,Sell,Buy,Price,Inverse Price,Date Updated,Status");
        for (var i = 1; i <= 10; i++)
        {
            if (i % 2 == 0)
            {
                sb.AppendLine($"LTCETH,Market,0.06 ETH,1.63850661 LTC,1 ETH = 27.3084516927144 LTC,1 LTC = 0.0366187 ETH,2023-11-{i:00} 14:02:16,Successful");
            }
            else
            {
                sb.AppendLine($"ETHWUSDT,Market,17.11711731 ETHW,46.55766488 USDT,1 ETHW = 2.71995 USDT,1 USDT = 0.3676538171657567 ETHW,2024-01-{i:00} 15:00:55,Successful");
            }
        }

        return sb.ToString();
    }
}
