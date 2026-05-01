using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRp.Migrations
{
    /// <inheritdoc />
    public partial class AddImageAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImageAssets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Bytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    UserPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FinalPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProviderModelId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageAssets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageAssets_ChatId_CreatedUtc",
                table: "ImageAssets",
                columns: new[] { "ChatId", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImageAssets");
        }
    }
}
