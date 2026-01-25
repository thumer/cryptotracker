using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddManualCoinPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManualCoinPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    PriceEur = table.Column<decimal>(type: "decimal(27,12)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualCoinPrices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManualCoinPrices_Symbol_Date",
                table: "ManualCoinPrices",
                columns: new[] { "Symbol", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManualCoinPrices");
        }
    }
}
