using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoTracker.Migrations
{
    /// <inheritdoc />
    public partial class TransactionLinkingAndLots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVirtual",
                table: "Wallets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsIntentionallyUnlinked",
                table: "CryptoTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

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

            migrationBuilder.AddColumn<int>(
                name: "SourceLotId",
                table: "CryptoTrades",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentMemories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemoryType = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsageCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentMemories", x => x.Id);
                });

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
                    IsFlowComplete = table.Column<bool>(type: "bit", nullable: false),
                    FlowIncompleteReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransformedToLotId = table.Column<int>(type: "int", nullable: true),
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
                        name: "FK_AssetLots_AssetLots_TransformedToLotId",
                        column: x => x.TransformedToLotId,
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
                name: "TransactionLinkMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionId = table.Column<int>(type: "int", nullable: false),
                    LinkType = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    LinkedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionLinkMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionLinkMetadata_CryptoTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "CryptoTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_CryptoTrades_SourceLotId",
                table: "CryptoTrades",
                column: "SourceLotId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMemories_AgentKey",
                table: "AgentMemories",
                column: "AgentKey");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMemories_AgentKey_MemoryType_Key",
                table: "AgentMemories",
                columns: new[] { "AgentKey", "MemoryType", "Key" },
                unique: true);

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
                name: "IX_AssetLots_TransformedToLotId",
                table: "AssetLots",
                column: "TransformedToLotId");

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

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLinkMetadata_IsConfirmed",
                table: "TransactionLinkMetadata",
                column: "IsConfirmed");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLinkMetadata_LinkType",
                table: "TransactionLinkMetadata",
                column: "LinkType");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLinkMetadata_TransactionId",
                table: "TransactionLinkMetadata",
                column: "TransactionId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CryptoTrades_AssetLots_ResultingLotId",
                table: "CryptoTrades",
                column: "ResultingLotId",
                principalTable: "AssetLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CryptoTrades_AssetLots_SourceLotId",
                table: "CryptoTrades",
                column: "SourceLotId",
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
                name: "FK_CryptoTrades_AssetLots_SourceLotId",
                table: "CryptoTrades");

            migrationBuilder.DropForeignKey(
                name: "FK_CryptoTransactions_AssetLots_ResultingLotId",
                table: "CryptoTransactions");

            migrationBuilder.DropTable(
                name: "AgentMemories");

            migrationBuilder.DropTable(
                name: "LotMovements");

            migrationBuilder.DropTable(
                name: "TransactionLinkMetadata");

            migrationBuilder.DropTable(
                name: "AssetLots");

            migrationBuilder.DropIndex(
                name: "IX_CryptoTransactions_ResultingLotId",
                table: "CryptoTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CryptoTrades_ResultingLotId",
                table: "CryptoTrades");

            migrationBuilder.DropIndex(
                name: "IX_CryptoTrades_SourceLotId",
                table: "CryptoTrades");

            migrationBuilder.DropColumn(
                name: "IsVirtual",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "IsIntentionallyUnlinked",
                table: "CryptoTransactions");

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

            migrationBuilder.DropColumn(
                name: "SourceLotId",
                table: "CryptoTrades");
        }
    }
}
