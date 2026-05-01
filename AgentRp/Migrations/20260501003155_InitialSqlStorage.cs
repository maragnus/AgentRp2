using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRp.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiProviders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Chats",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Updated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Starred = table.Column<bool>(type: "bit", nullable: false),
                    Messages = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiProviderModels",
                columns: table => new
                {
                    ProviderId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Text = table.Column<bool>(type: "bit", nullable: false),
                    Image = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviderModels", x => new { x.ProviderId, x.Id });
                    table.ForeignKey(
                        name: "FK_AiProviderModels_AiProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "AiProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatDocuments",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CharactersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocationsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimelineJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MessagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PromptLibraryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelTuningJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatDocuments", x => x.ChatId);
                    table.ForeignKey(
                        name: "FK_ChatDocuments_Chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "Chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderModels_SortOrder",
                table: "AiProviderModels",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_AiProviders_SortOrder",
                table: "AiProviders",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_SortOrder",
                table: "Chats",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_UpdatedUtc",
                table: "Chats",
                column: "UpdatedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiProviderModels");

            migrationBuilder.DropTable(
                name: "ChatDocuments");

            migrationBuilder.DropTable(
                name: "AiProviders");

            migrationBuilder.DropTable(
                name: "Chats");
        }
    }
}
