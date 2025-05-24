using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoTracker.Migrations
{
    /// <inheritdoc />
    public partial class WalletRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OppositeWallet",
                table: "CryptoTransactions");

            migrationBuilder.DropColumn(
                name: "Wallet",
                table: "CryptoTransactions");

            migrationBuilder.DropColumn(
                name: "Wallet",
                table: "CryptoTrades");

            migrationBuilder.AddColumn<int>(
                name: "OppositeWalletId",
                table: "CryptoTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WalletId",
                table: "CryptoTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WalletId",
                table: "CryptoTrades",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Timestamp",
                table: "BitpandaTransactions",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "SpreadCurrency",
                table: "BitpandaTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FeeAsset",
                table: "BitpandaTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Wallets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CryptoTransactions_OppositeWalletId",
                table: "CryptoTransactions",
                column: "OppositeWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_CryptoTransactions_WalletId",
                table: "CryptoTransactions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_CryptoTrades_WalletId",
                table: "CryptoTrades",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_Name",
                table: "Wallets",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CryptoTrades_Wallets_WalletId",
                table: "CryptoTrades",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CryptoTransactions_Wallets_OppositeWalletId",
                table: "CryptoTransactions",
                column: "OppositeWalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CryptoTransactions_Wallets_WalletId",
                table: "CryptoTransactions",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CryptoTrades_Wallets_WalletId",
                table: "CryptoTrades");

            migrationBuilder.DropForeignKey(
                name: "FK_CryptoTransactions_Wallets_OppositeWalletId",
                table: "CryptoTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_CryptoTransactions_Wallets_WalletId",
                table: "CryptoTransactions");

            migrationBuilder.DropTable(
                name: "Wallets");

            migrationBuilder.DropIndex(
                name: "IX_CryptoTransactions_OppositeWalletId",
                table: "CryptoTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CryptoTransactions_WalletId",
                table: "CryptoTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CryptoTrades_WalletId",
                table: "CryptoTrades");

            migrationBuilder.DropColumn(
                name: "OppositeWalletId",
                table: "CryptoTransactions");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "CryptoTransactions");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "CryptoTrades");

            migrationBuilder.AddColumn<string>(
                name: "OppositeWallet",
                table: "CryptoTransactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Wallet",
                table: "CryptoTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Wallet",
                table: "CryptoTrades",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "BitpandaTransactions",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<decimal>(
                name: "SpreadCurrency",
                table: "BitpandaTransactions",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<decimal>(
                name: "FeeAsset",
                table: "BitpandaTransactions",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
