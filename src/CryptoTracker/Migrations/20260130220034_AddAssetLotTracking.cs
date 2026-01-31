using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetLotTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LotAssignmentConfirmed",
                table: "CryptoTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ResultingLotId",
                table: "CryptoTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LotAssignmentConfirmed",
                table: "CryptoTrades",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ResultingLotId",
                table: "CryptoTrades",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssetLots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CurrentWalletId = table.Column<int>(type: "int", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(27,12)", nullable: false),
                    OriginalQuantity = table.Column<decimal>(type: "decimal(27,12)", nullable: false),
                    AcquisitionDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcquisitionPriceEur = table.Column<decimal>(type: "decimal(27,12)", nullable: false),
                    TotalAcquisitionCostEur = table.Column<decimal>(type: "decimal(27,12)", nullable: false),
                    AcquisitionType = table.Column<int>(type: "int", nullable: false),
                    SourceTransactionId = table.Column<int>(type: "int", nullable: true),
                    SourceTradeId = table.Column<int>(type: "int", nullable: true),
                    ParentLotId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetLots_AssetLots_ParentLotId",
                        column: x => x.ParentLotId,
                        principalTable: "AssetLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetLots_CryptoTrades_SourceTradeId",
                        column: x => x.SourceTradeId,
                        principalTable: "CryptoTrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetLots_CryptoTransactions_SourceTransactionId",
                        column: x => x.SourceTransactionId,
                        principalTable: "CryptoTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetLots_Wallets_CurrentWalletId",
                        column: x => x.CurrentWalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LotMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LotId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(27,12)", nullable: false),
                    DateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MovementType = table.Column<int>(type: "int", nullable: false),
                    TradeId = table.Column<int>(type: "int", nullable: true),
                    TransactionId = table.Column<int>(type: "int", nullable: true),
                    SalePriceEur = table.Column<decimal>(type: "decimal(27,12)", nullable: true),
                    RealizedGainEur = table.Column<decimal>(type: "decimal(27,12)", nullable: true),
                    IsTaxFree = table.Column<bool>(type: "bit", nullable: false),
                    TaxFreeReason = table.Column<int>(type: "int", nullable: true),
                    ResultingLotId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotMovements_AssetLots_LotId",
                        column: x => x.LotId,
                        principalTable: "AssetLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LotMovements_AssetLots_ResultingLotId",
                        column: x => x.ResultingLotId,
                        principalTable: "AssetLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotMovements_CryptoTrades_TradeId",
                        column: x => x.TradeId,
                        principalTable: "CryptoTrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotMovements_CryptoTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "CryptoTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CryptoTransactions_ResultingLotId",
                table: "CryptoTransactions",
                column: "ResultingLotId");

            migrationBuilder.CreateIndex(
                name: "IX_CryptoTrades_ResultingLotId",
                table: "CryptoTrades",
                column: "ResultingLotId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetLots_AcquisitionDate",
                table: "AssetLots",
                column: "AcquisitionDate");

            migrationBuilder.CreateIndex(
                name: "IX_AssetLots_CurrentWalletId_Symbol",
                table: "AssetLots",
                columns: new[] { "CurrentWalletId", "Symbol" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetLots_ParentLotId",
                table: "AssetLots",
                column: "ParentLotId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetLots_SourceTradeId",
                table: "AssetLots",
                column: "SourceTradeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetLots_SourceTransactionId",
                table: "AssetLots",
                column: "SourceTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_LotMovements_DateTime",
                table: "LotMovements",
                column: "DateTime");

            migrationBuilder.CreateIndex(
                name: "IX_LotMovements_LotId",
                table: "LotMovements",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_LotMovements_ResultingLotId",
                table: "LotMovements",
                column: "ResultingLotId");

            migrationBuilder.CreateIndex(
                name: "IX_LotMovements_TradeId",
                table: "LotMovements",
                column: "TradeId");

            migrationBuilder.CreateIndex(
                name: "IX_LotMovements_TransactionId",
                table: "LotMovements",
                column: "TransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_CryptoTrades_AssetLots_ResultingLotId",
                table: "CryptoTrades",
                column: "ResultingLotId",
                principalTable: "AssetLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CryptoTransactions_AssetLots_ResultingLotId",
                table: "CryptoTransactions",
                column: "ResultingLotId",
                principalTable: "AssetLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CryptoTrades_AssetLots_ResultingLotId",
                table: "CryptoTrades");

            migrationBuilder.DropForeignKey(
                name: "FK_CryptoTransactions_AssetLots_ResultingLotId",
                table: "CryptoTransactions");

            migrationBuilder.DropTable(
                name: "LotMovements");

            migrationBuilder.DropTable(
                name: "AssetLots");

            migrationBuilder.DropIndex(
                name: "IX_CryptoTransactions_ResultingLotId",
                table: "CryptoTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CryptoTrades_ResultingLotId",
                table: "CryptoTrades");

            migrationBuilder.DropColumn(
                name: "LotAssignmentConfirmed",
                table: "CryptoTransactions");

            migrationBuilder.DropColumn(
                name: "ResultingLotId",
                table: "CryptoTransactions");

            migrationBuilder.DropColumn(
                name: "LotAssignmentConfirmed",
                table: "CryptoTrades");

            migrationBuilder.DropColumn(
                name: "ResultingLotId",
                table: "CryptoTrades");
        }
    }
}
