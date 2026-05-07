using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRp.Migrations
{
    /// <inheritdoc />
    public partial class AddElevenLabsVoiceCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ElevenLabsVoiceCatalog",
                columns: table => new
                {
                    VoiceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PublicOwnerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DateUnix = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreviewUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Featured = table.Column<bool>(type: "bit", nullable: false),
                    Accent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Age = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UseCase = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descriptive = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    VerifiedLanguagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsBookmarked = table.Column<bool>(type: "bit", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElevenLabsVoiceCatalog", x => x.VoiceId);
                });

            migrationBuilder.CreateTable(
                name: "ElevenLabsVoiceCatalogStates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LastRefreshUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRefreshError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    TotalCount = table.Column<int>(type: "int", nullable: false),
                    CachedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElevenLabsVoiceCatalogStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElevenLabsVoiceCatalog_IsAvailable",
                table: "ElevenLabsVoiceCatalog",
                column: "IsAvailable");

            migrationBuilder.CreateIndex(
                name: "IX_ElevenLabsVoiceCatalog_IsBookmarked",
                table: "ElevenLabsVoiceCatalog",
                column: "IsBookmarked");

            migrationBuilder.CreateIndex(
                name: "IX_ElevenLabsVoiceCatalog_Name",
                table: "ElevenLabsVoiceCatalog",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElevenLabsVoiceCatalog");

            migrationBuilder.DropTable(
                name: "ElevenLabsVoiceCatalogStates");
        }
    }
}
