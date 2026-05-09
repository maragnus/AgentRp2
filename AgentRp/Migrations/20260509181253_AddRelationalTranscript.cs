using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRp.Migrations
{
    /// <inheritdoc />
    public partial class AddRelationalTranscript : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveLeafTurnId",
                table: "Chats",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ActiveLocationId",
                table: "Chats",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ActiveLocationName",
                table: "Chats",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ActiveTurnCount",
                table: "Chats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SnapshotCount",
                table: "Chats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ChatCurrentSceneCharacters",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CharacterId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatCurrentSceneCharacters", x => new { x.ChatId, x.CharacterId });
                });

            migrationBuilder.CreateTable(
                name: "TranscriptSnapshots",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TurnId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StartTurnId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EndTurnId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ParentBeforeStartTurnId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TurnNumberStart = table.Column<int>(type: "int", nullable: false),
                    TurnNumberEnd = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SceneLocationId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SceneLocationName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SceneJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpeechJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrivateIntentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CharacterAppearancesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TraceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConsumedBySnapshotId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ConsumedBySnapshotOrdinal = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranscriptSnapshots", x => new { x.ChatId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "TranscriptTurns",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ParentTurnId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TurnNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AuthorCharacterId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActorCharacterId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Guidance = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SceneLocationId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SceneLocationName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SceneJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlanJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AppearanceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrivateIntentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpeechJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TraceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConsumedBySnapshotId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ConsumedBySnapshotOrdinal = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranscriptTurns", x => new { x.ChatId, x.Id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatCurrentSceneCharacters_ChatId_SortOrder",
                table: "ChatCurrentSceneCharacters",
                columns: new[] { "ChatId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptSnapshots_ChatId_ConsumedBySnapshotId_ConsumedBySnapshotOrdinal",
                table: "TranscriptSnapshots",
                columns: new[] { "ChatId", "ConsumedBySnapshotId", "ConsumedBySnapshotOrdinal" });

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptSnapshots_ChatId_EndTurnId_IsActive",
                table: "TranscriptSnapshots",
                columns: new[] { "ChatId", "EndTurnId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptTurns_ChatId_ConsumedBySnapshotId_ConsumedBySnapshotOrdinal",
                table: "TranscriptTurns",
                columns: new[] { "ChatId", "ConsumedBySnapshotId", "ConsumedBySnapshotOrdinal" });

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptTurns_ChatId_ParentTurnId",
                table: "TranscriptTurns",
                columns: new[] { "ChatId", "ParentTurnId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatCurrentSceneCharacters");

            migrationBuilder.DropTable(
                name: "TranscriptSnapshots");

            migrationBuilder.DropTable(
                name: "TranscriptTurns");

            migrationBuilder.DropColumn(
                name: "ActiveLeafTurnId",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "ActiveLocationId",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "ActiveLocationName",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "ActiveTurnCount",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "SnapshotCount",
                table: "Chats");
        }
    }
}
