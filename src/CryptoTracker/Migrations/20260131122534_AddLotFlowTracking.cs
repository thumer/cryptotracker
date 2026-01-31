using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddLotFlowTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceLotId",
                table: "CryptoTrades",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FlowIncompleteReason",
                table: "AssetLots",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFlowComplete",
                table: "AssetLots",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TransformedToLotId",
                table: "AssetLots",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CryptoTrades_SourceLotId",
                table: "CryptoTrades",
                column: "SourceLotId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetLots_TransformedToLotId",
                table: "AssetLots",
                column: "TransformedToLotId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetLots_AssetLots_TransformedToLotId",
                table: "AssetLots",
                column: "TransformedToLotId",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetLots_AssetLots_TransformedToLotId",
                table: "AssetLots");

            migrationBuilder.DropForeignKey(
                name: "FK_CryptoTrades_AssetLots_SourceLotId",
                table: "CryptoTrades");

            migrationBuilder.DropIndex(
                name: "IX_CryptoTrades_SourceLotId",
                table: "CryptoTrades");

            migrationBuilder.DropIndex(
                name: "IX_AssetLots_TransformedToLotId",
                table: "AssetLots");

            migrationBuilder.DropColumn(
                name: "SourceLotId",
                table: "CryptoTrades");

            migrationBuilder.DropColumn(
                name: "FlowIncompleteReason",
                table: "AssetLots");

            migrationBuilder.DropColumn(
                name: "IsFlowComplete",
                table: "AssetLots");

            migrationBuilder.DropColumn(
                name: "TransformedToLotId",
                table: "AssetLots");
        }
    }
}
