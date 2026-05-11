using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRp.Migrations
{
    /// <inheritdoc />
    public partial class StoryCardsV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoryCardHistory",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StoryCardInstanceId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: false),
                    TurnNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryCardHistory", x => new { x.ChatId, x.StoryCardInstanceId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "StoryCardInstanceItems",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StoryCardInstanceId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceTemplateChildId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectionInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OngoingContext = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryCardInstanceItems", x => new { x.ChatId, x.StoryCardInstanceId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "StoryCardInstanceLocations",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StoryCardInstanceId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceTemplateChildId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectionInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OngoingContext = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryCardInstanceLocations", x => new { x.ChatId, x.StoryCardInstanceId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "StoryCardInstancePhases",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StoryCardInstanceId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceTemplateChildId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SetupInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlanningContext = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EndCondition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsOptional = table.Column<bool>(type: "bit", nullable: false),
                    IsEnding = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryCardInstancePhases", x => new { x.ChatId, x.StoryCardInstanceId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "StoryCardInstancePhaseTransitions",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StoryCardInstanceId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FromPhaseId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToPhaseId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConditionInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryCardInstancePhaseTransitions", x => new { x.ChatId, x.StoryCardInstanceId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "StoryCardInstanceRoles",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StoryCardInstanceId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceTemplateChildId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectionInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OngoingContext = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrivateContext = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryCardInstanceRoles", x => new { x.ChatId, x.StoryCardInstanceId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "StoryCardInstances",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceTemplateId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ParentTemplateId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RootTemplateId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceOwnerDisplayName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StartTurnNumber = table.Column<int>(type: "int", nullable: false),
                    EndTurnNumber = table.Column<int>(type: "int", nullable: true),
                    ActivePhaseId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryCardInstances", x => new { x.ChatId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "StoryCardTemplateItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StoryCardTemplateId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectionInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OngoingContext = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryCardTemplateItems", x => new { x.StoryCardTemplateId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "StoryCardTemplateLocations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StoryCardTemplateId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectionInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OngoingContext = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryCardTemplateLocations", x => new { x.StoryCardTemplateId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "StoryCardTemplatePhases",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StoryCardTemplateId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SetupInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlanningContext = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EndCondition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsOptional = table.Column<bool>(type: "bit", nullable: false),
                    IsEnding = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryCardTemplatePhases", x => new { x.StoryCardTemplateId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "StoryCardTemplatePhaseTransitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StoryCardTemplateId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FromPhaseId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToPhaseId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConditionInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryCardTemplatePhaseTransitions", x => new { x.StoryCardTemplateId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "StoryCardTemplateRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StoryCardTemplateId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectionInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OngoingContext = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrivateContext = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryCardTemplateRoles", x => new { x.StoryCardTemplateId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "StoryCardTemplates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerDisplayName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsShared = table.Column<bool>(type: "bit", nullable: false),
                    RetiredUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ParentTemplateId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RootTemplateId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    DirectStoryCount = table.Column<int>(type: "int", nullable: false),
                    DirectActiveTurnCount = table.Column<int>(type: "int", nullable: false),
                    RemixCount = table.Column<int>(type: "int", nullable: false),
                    RemixStoryCount = table.Column<int>(type: "int", nullable: false),
                    RemixActiveTurnCount = table.Column<int>(type: "int", nullable: false),
                    StatsRefreshedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryCardTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardHistory_ChatId_StoryCardInstanceId_CreatedUtc",
                table: "StoryCardHistory",
                columns: new[] { "ChatId", "StoryCardInstanceId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardInstanceItems_ChatId_StoryCardInstanceId_SortOrder",
                table: "StoryCardInstanceItems",
                columns: new[] { "ChatId", "StoryCardInstanceId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardInstanceLocations_ChatId_StoryCardInstanceId_SortOrder",
                table: "StoryCardInstanceLocations",
                columns: new[] { "ChatId", "StoryCardInstanceId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardInstancePhases_ChatId_StoryCardInstanceId_SortOrder",
                table: "StoryCardInstancePhases",
                columns: new[] { "ChatId", "StoryCardInstanceId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardInstancePhaseTransitions_ChatId_StoryCardInstanceId_SortOrder",
                table: "StoryCardInstancePhaseTransitions",
                columns: new[] { "ChatId", "StoryCardInstanceId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardInstanceRoles_ChatId_StoryCardInstanceId_SortOrder",
                table: "StoryCardInstanceRoles",
                columns: new[] { "ChatId", "StoryCardInstanceId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardInstances_ChatId_SortOrder",
                table: "StoryCardInstances",
                columns: new[] { "ChatId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardInstances_SourceTemplateId",
                table: "StoryCardInstances",
                column: "SourceTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardTemplateItems_StoryCardTemplateId_SortOrder",
                table: "StoryCardTemplateItems",
                columns: new[] { "StoryCardTemplateId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardTemplateLocations_StoryCardTemplateId_SortOrder",
                table: "StoryCardTemplateLocations",
                columns: new[] { "StoryCardTemplateId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardTemplatePhases_StoryCardTemplateId_SortOrder",
                table: "StoryCardTemplatePhases",
                columns: new[] { "StoryCardTemplateId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardTemplatePhaseTransitions_StoryCardTemplateId_SortOrder",
                table: "StoryCardTemplatePhaseTransitions",
                columns: new[] { "StoryCardTemplateId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardTemplateRoles_StoryCardTemplateId_SortOrder",
                table: "StoryCardTemplateRoles",
                columns: new[] { "StoryCardTemplateId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardTemplates_IsShared",
                table: "StoryCardTemplates",
                column: "IsShared");

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardTemplates_OwnerUserId",
                table: "StoryCardTemplates",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardTemplates_ParentTemplateId",
                table: "StoryCardTemplates",
                column: "ParentTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardTemplates_RootTemplateId",
                table: "StoryCardTemplates",
                column: "RootTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoryCardHistory");

            migrationBuilder.DropTable(
                name: "StoryCardInstanceItems");

            migrationBuilder.DropTable(
                name: "StoryCardInstanceLocations");

            migrationBuilder.DropTable(
                name: "StoryCardInstancePhases");

            migrationBuilder.DropTable(
                name: "StoryCardInstancePhaseTransitions");

            migrationBuilder.DropTable(
                name: "StoryCardInstanceRoles");

            migrationBuilder.DropTable(
                name: "StoryCardInstances");

            migrationBuilder.DropTable(
                name: "StoryCardTemplateItems");

            migrationBuilder.DropTable(
                name: "StoryCardTemplateLocations");

            migrationBuilder.DropTable(
                name: "StoryCardTemplatePhases");

            migrationBuilder.DropTable(
                name: "StoryCardTemplatePhaseTransitions");

            migrationBuilder.DropTable(
                name: "StoryCardTemplateRoles");

            migrationBuilder.DropTable(
                name: "StoryCardTemplates");
        }
    }
}
