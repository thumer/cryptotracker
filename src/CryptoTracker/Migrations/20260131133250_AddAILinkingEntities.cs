using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddAILinkingEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsIntentionallyUnlinked",
                table: "CryptoTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentMemories");

            migrationBuilder.DropTable(
                name: "TransactionLinkMetadata");

            migrationBuilder.DropColumn(
                name: "IsIntentionallyUnlinked",
                table: "CryptoTransactions");
        }
    }
}
