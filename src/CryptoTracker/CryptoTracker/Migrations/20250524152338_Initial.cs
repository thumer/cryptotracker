using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoTracker.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BinanceDeposits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Coin = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Network = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TXID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BinanceDeposits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BinanceTrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Pair = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Side = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Executed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Fee = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BinanceTrades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BinanceWithdrawals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Coin = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Network = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TXID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BinanceWithdrawals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BitcoinDeTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Datum = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Typ = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Waehrung = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Referenz = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Adresse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Kurs = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EinheitKurs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CryptoVorGebuehr = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MengeVorGebuehr = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EinheitMengeVorGebuehr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CryptoNachGebuehr = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MengeNachGebuehr = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EinheitMengeNachGebuehr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ZuAbgang = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Kontostand = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Kommentar = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BitcoinDeTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BitpandaTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InOut = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AmountFiat = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Fiat = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AmountAsset = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Asset = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssetMarketPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AssetMarketPriceCurrency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssetClass = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    Fee = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FeeAsset = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Spread = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SpreadCurrency = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxFiat = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BitpandaTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CryptoTrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Wallet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OpositeSymbol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TradeType = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(27,12)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(27,12)", nullable: false),
                    Fee = table.Column<decimal>(type: "decimal(27,12)", nullable: false),
                    ForeignFee = table.Column<decimal>(type: "decimal(27,12)", nullable: false),
                    ForeignFeeSymbol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Referenz = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OppositeTradeId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CryptoTrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CryptoTrades_CryptoTrades_OppositeTradeId",
                        column: x => x.OppositeTradeId,
                        principalTable: "CryptoTrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CryptoTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Wallet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(27,12)", nullable: false),
                    Fee = table.Column<decimal>(type: "decimal(27,12)", nullable: false),
                    OppositeTransactionId = table.Column<int>(type: "int", nullable: true),
                    OppositeWallet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Network = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CryptoTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CryptoTransactions_CryptoTransactions_OppositeTransactionId",
                        column: x => x.OppositeTransactionId,
                        principalTable: "CryptoTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MetamaskTrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Pair = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Side = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Executed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Fee = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tradingplatform = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetamaskTrades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetamaskTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Datum = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Typ = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Coin = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Network = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Kommentar = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetamaskTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OkxDeposits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Coin = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Network = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Kommentar = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OkxDeposits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OkxTrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Pair = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Side = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Executed = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OkxTrades", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CryptoTrades_OppositeTradeId",
                table: "CryptoTrades",
                column: "OppositeTradeId",
                unique: true,
                filter: "[OppositeTradeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CryptoTransactions_OppositeTransactionId",
                table: "CryptoTransactions",
                column: "OppositeTransactionId",
                unique: true,
                filter: "[OppositeTransactionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BinanceDeposits");

            migrationBuilder.DropTable(
                name: "BinanceTrades");

            migrationBuilder.DropTable(
                name: "BinanceWithdrawals");

            migrationBuilder.DropTable(
                name: "BitcoinDeTransactions");

            migrationBuilder.DropTable(
                name: "BitpandaTransactions");

            migrationBuilder.DropTable(
                name: "CryptoTrades");

            migrationBuilder.DropTable(
                name: "CryptoTransactions");

            migrationBuilder.DropTable(
                name: "MetamaskTrades");

            migrationBuilder.DropTable(
                name: "MetamaskTransactions");

            migrationBuilder.DropTable(
                name: "OkxDeposits");

            migrationBuilder.DropTable(
                name: "OkxTrades");
        }
    }
}
