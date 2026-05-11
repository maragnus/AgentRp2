using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRp.Migrations
{
    /// <inheritdoc />
    public partial class StoryCardPhaseRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "StoryCardInstanceRoles");

            migrationBuilder.DropColumn(
                name: "EntityName",
                table: "StoryCardInstanceRoles");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "StoryCardInstanceLocations");

            migrationBuilder.DropColumn(
                name: "EntityName",
                table: "StoryCardInstanceLocations");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "StoryCardInstanceItems");

            migrationBuilder.DropColumn(
                name: "EntityName",
                table: "StoryCardInstanceItems");

            migrationBuilder.CreateTable(
                name: "StoryCardInstanceAssignments",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StoryCardInstanceId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ChildCardType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ChildCardId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryCardInstanceAssignments", x => new { x.ChatId, x.StoryCardInstanceId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "StoryCardInstancePhaseRequirements",
                columns: table => new
                {
                    ChatId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StoryCardInstanceId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceTemplateChildId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PhaseId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ChildCardType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ChildCardId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RequiredCount = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryCardInstancePhaseRequirements", x => new { x.ChatId, x.StoryCardInstanceId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "StoryCardTemplatePhaseRequirements",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StoryCardTemplateId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PhaseId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ChildCardType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ChildCardId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RequiredCount = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryCardTemplatePhaseRequirements", x => new { x.StoryCardTemplateId, x.Id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardInstanceAssignments_ChatId_StoryCardInstanceId_SortOrder",
                table: "StoryCardInstanceAssignments",
                columns: new[] { "ChatId", "StoryCardInstanceId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardInstancePhaseRequirements_ChatId_StoryCardInstanceId_SortOrder",
                table: "StoryCardInstancePhaseRequirements",
                columns: new[] { "ChatId", "StoryCardInstanceId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryCardTemplatePhaseRequirements_StoryCardTemplateId_SortOrder",
                table: "StoryCardTemplatePhaseRequirements",
                columns: new[] { "StoryCardTemplateId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoryCardInstanceAssignments");

            migrationBuilder.DropTable(
                name: "StoryCardInstancePhaseRequirements");

            migrationBuilder.DropTable(
                name: "StoryCardTemplatePhaseRequirements");

            migrationBuilder.AddColumn<string>(
                name: "EntityId",
                table: "StoryCardInstanceRoles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntityName",
                table: "StoryCardInstanceRoles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntityId",
                table: "StoryCardInstanceLocations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntityName",
                table: "StoryCardInstanceLocations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntityId",
                table: "StoryCardInstanceItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntityName",
                table: "StoryCardInstanceItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
