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

            migrationBuilder.CreateTable(
                name: "BinanceDeposits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<int>(type: "int", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_BinanceDeposits_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BinanceTrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<int>(type: "int", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_BinanceTrades_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BinanceWithdrawals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<int>(type: "int", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_BinanceWithdrawals_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BitcoinDeTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<int>(type: "int", nullable: false),
                    Datum = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_BitcoinDeTransactions_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BitpandaTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<int>(type: "int", nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
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
                    FeeAsset = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Spread = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SpreadCurrency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TaxFiat = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BitpandaTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BitpandaTransactions_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CryptoTrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<int>(type: "int", nullable: false),
                    DateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_CryptoTrades_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CryptoTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<int>(type: "int", nullable: false),
                    DateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(27,12)", nullable: false),
                    Fee = table.Column<decimal>(type: "decimal(27,12)", nullable: false),
                    OppositeTransactionId = table.Column<int>(type: "int", nullable: true),
                    OppositeWalletId = table.Column<int>(type: "int", nullable: true),
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
                    table.ForeignKey(
                        name: "FK_CryptoTransactions_Wallets_OppositeWalletId",
                        column: x => x.OppositeWalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CryptoTransactions_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetamaskTrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<int>(type: "int", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_MetamaskTrades_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetamaskTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    From = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    To = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValueInUSD = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionFee = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionFeeInUSD = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GasPrice = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GasLimit = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetamaskTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetamaskTransactions_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OkxDeposits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_OkxDeposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OkxDeposits_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OkxTrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Pair = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Side = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Executed = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OkxTrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OkxTrades_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BinanceDeposits_WalletId",
                table: "BinanceDeposits",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BinanceTrades_WalletId",
                table: "BinanceTrades",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BinanceWithdrawals_WalletId",
                table: "BinanceWithdrawals",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BitcoinDeTransactions_WalletId",
                table: "BitcoinDeTransactions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BitpandaTransactions_WalletId",
                table: "BitpandaTransactions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_CryptoTrades_OppositeTradeId",
                table: "CryptoTrades",
                column: "OppositeTradeId",
                unique: true,
                filter: "[OppositeTradeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CryptoTrades_WalletId",
                table: "CryptoTrades",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_CryptoTransactions_OppositeTransactionId",
                table: "CryptoTransactions",
                column: "OppositeTransactionId",
                unique: true,
                filter: "[OppositeTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CryptoTransactions_OppositeWalletId",
                table: "CryptoTransactions",
                column: "OppositeWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_CryptoTransactions_WalletId",
                table: "CryptoTransactions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_MetamaskTrades_WalletId",
                table: "MetamaskTrades",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_MetamaskTransactions_WalletId",
                table: "MetamaskTransactions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_OkxDeposits_WalletId",
                table: "OkxDeposits",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_OkxTrades_WalletId",
                table: "OkxTrades",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_Name",
                table: "Wallets",
                column: "Name",
                unique: true);
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

            migrationBuilder.DropTable(
                name: "Wallets");
        }
    }
}
