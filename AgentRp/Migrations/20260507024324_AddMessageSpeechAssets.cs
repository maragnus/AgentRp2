using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRp.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageSpeechAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpeechAssets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TurnId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Bytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProviderId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProviderType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderModelId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VoiceIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpeechAssets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpeechAssets_ChatId_CreatedUtc",
                table: "SpeechAssets",
                columns: new[] { "ChatId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SpeechAssets_ChatId_TurnId",
                table: "SpeechAssets",
                columns: new[] { "ChatId", "TurnId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpeechAssets");
        }
    }
}
