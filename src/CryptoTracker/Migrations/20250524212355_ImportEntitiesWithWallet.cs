using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoTracker.Migrations
{
    /// <inheritdoc />
    public partial class ImportEntitiesWithWallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount",
                table: "MetamaskTransactions");

            migrationBuilder.RenameColumn(
                name: "Kommentar",
                table: "OkxDeposits",
                newName: "TXID");

            migrationBuilder.RenameColumn(
                name: "Typ",
                table: "MetamaskTransactions",
                newName: "ValueInUSD");

            migrationBuilder.RenameColumn(
                name: "Network",
                table: "MetamaskTransactions",
                newName: "Value");

            migrationBuilder.RenameColumn(
                name: "Kommentar",
                table: "MetamaskTransactions",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "Datum",
                table: "MetamaskTransactions",
                newName: "Date");

            migrationBuilder.RenameColumn(
                name: "Coin",
                table: "MetamaskTransactions",
                newName: "TransactionFeeInUSD");

            migrationBuilder.AddColumn<int>(
                name: "WalletId",
                table: "OkxTrades",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "OkxDeposits",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionFee",
                table: "OkxDeposits",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "WalletId",
                table: "OkxDeposits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "TransactionFee",
                table: "MetamaskTransactions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<string>(
                name: "From",
                table: "MetamaskTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GasLimit",
                table: "MetamaskTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GasPrice",
                table: "MetamaskTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Hash",
                table: "MetamaskTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "To",
                table: "MetamaskTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "WalletId",
                table: "MetamaskTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WalletId",
                table: "MetamaskTrades",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WalletId",
                table: "BitpandaTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WalletId",
                table: "BitcoinDeTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WalletId",
                table: "BinanceWithdrawals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WalletId",
                table: "BinanceTrades",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WalletId",
                table: "BinanceDeposits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_OkxTrades_WalletId",
                table: "OkxTrades",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_OkxDeposits_WalletId",
                table: "OkxDeposits",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_MetamaskTransactions_WalletId",
                table: "MetamaskTransactions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_MetamaskTrades_WalletId",
                table: "MetamaskTrades",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BitpandaTransactions_WalletId",
                table: "BitpandaTransactions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BitcoinDeTransactions_WalletId",
                table: "BitcoinDeTransactions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BinanceWithdrawals_WalletId",
                table: "BinanceWithdrawals",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BinanceTrades_WalletId",
                table: "BinanceTrades",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BinanceDeposits_WalletId",
                table: "BinanceDeposits",
                column: "WalletId");

            migrationBuilder.AddForeignKey(
                name: "FK_BinanceDeposits_Wallets_WalletId",
                table: "BinanceDeposits",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BinanceTrades_Wallets_WalletId",
                table: "BinanceTrades",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BinanceWithdrawals_Wallets_WalletId",
                table: "BinanceWithdrawals",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BitcoinDeTransactions_Wallets_WalletId",
                table: "BitcoinDeTransactions",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BitpandaTransactions_Wallets_WalletId",
                table: "BitpandaTransactions",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MetamaskTrades_Wallets_WalletId",
                table: "MetamaskTrades",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MetamaskTransactions_Wallets_WalletId",
                table: "MetamaskTransactions",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OkxDeposits_Wallets_WalletId",
                table: "OkxDeposits",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OkxTrades_Wallets_WalletId",
                table: "OkxTrades",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BinanceDeposits_Wallets_WalletId",
                table: "BinanceDeposits");

            migrationBuilder.DropForeignKey(
                name: "FK_BinanceTrades_Wallets_WalletId",
                table: "BinanceTrades");

            migrationBuilder.DropForeignKey(
                name: "FK_BinanceWithdrawals_Wallets_WalletId",
                table: "BinanceWithdrawals");

            migrationBuilder.DropForeignKey(
                name: "FK_BitcoinDeTransactions_Wallets_WalletId",
                table: "BitcoinDeTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_BitpandaTransactions_Wallets_WalletId",
                table: "BitpandaTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_MetamaskTrades_Wallets_WalletId",
                table: "MetamaskTrades");

            migrationBuilder.DropForeignKey(
                name: "FK_MetamaskTransactions_Wallets_WalletId",
                table: "MetamaskTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_OkxDeposits_Wallets_WalletId",
                table: "OkxDeposits");

            migrationBuilder.DropForeignKey(
                name: "FK_OkxTrades_Wallets_WalletId",
                table: "OkxTrades");

            migrationBuilder.DropIndex(
                name: "IX_OkxTrades_WalletId",
                table: "OkxTrades");

            migrationBuilder.DropIndex(
                name: "IX_OkxDeposits_WalletId",
                table: "OkxDeposits");

            migrationBuilder.DropIndex(
                name: "IX_MetamaskTransactions_WalletId",
                table: "MetamaskTransactions");

            migrationBuilder.DropIndex(
                name: "IX_MetamaskTrades_WalletId",
                table: "MetamaskTrades");

            migrationBuilder.DropIndex(
                name: "IX_BitpandaTransactions_WalletId",
                table: "BitpandaTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BitcoinDeTransactions_WalletId",
                table: "BitcoinDeTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BinanceWithdrawals_WalletId",
                table: "BinanceWithdrawals");

            migrationBuilder.DropIndex(
                name: "IX_BinanceTrades_WalletId",
                table: "BinanceTrades");

            migrationBuilder.DropIndex(
                name: "IX_BinanceDeposits_WalletId",
                table: "BinanceDeposits");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "OkxTrades");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "OkxDeposits");

            migrationBuilder.DropColumn(
                name: "TransactionFee",
                table: "OkxDeposits");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "OkxDeposits");

            migrationBuilder.DropColumn(
                name: "From",
                table: "MetamaskTransactions");

            migrationBuilder.DropColumn(
                name: "GasLimit",
                table: "MetamaskTransactions");

            migrationBuilder.DropColumn(
                name: "GasPrice",
                table: "MetamaskTransactions");

            migrationBuilder.DropColumn(
                name: "Hash",
                table: "MetamaskTransactions");

            migrationBuilder.DropColumn(
                name: "To",
                table: "MetamaskTransactions");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "MetamaskTransactions");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "MetamaskTrades");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "BitpandaTransactions");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "BitcoinDeTransactions");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "BinanceWithdrawals");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "BinanceTrades");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "BinanceDeposits");

            migrationBuilder.RenameColumn(
                name: "TXID",
                table: "OkxDeposits",
                newName: "Kommentar");

            migrationBuilder.RenameColumn(
                name: "ValueInUSD",
                table: "MetamaskTransactions",
                newName: "Typ");

            migrationBuilder.RenameColumn(
                name: "Value",
                table: "MetamaskTransactions",
                newName: "Network");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "MetamaskTransactions",
                newName: "Kommentar");

            migrationBuilder.RenameColumn(
                name: "TransactionFeeInUSD",
                table: "MetamaskTransactions",
                newName: "Coin");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "MetamaskTransactions",
                newName: "Datum");

            migrationBuilder.AlterColumn<decimal>(
                name: "TransactionFee",
                table: "MetamaskTransactions",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "MetamaskTransactions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
