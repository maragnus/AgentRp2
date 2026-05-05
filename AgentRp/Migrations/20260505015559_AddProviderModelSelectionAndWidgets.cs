using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRp.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderModelSelectionAndWidgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountId",
                table: "AiProviders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastMetricsError",
                table: "AiProviders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMetricsRefreshUtc",
                table: "AiProviders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagementApiKey",
                table: "AiProviders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProjectId",
                table: "AiProviders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TeamId",
                table: "AiProviders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "ActiveText",
                table: "AiProviderModels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "AiProviderModels",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Endpoint",
                table: "AiProviderModels",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Repository",
                table: "AiProviderModels",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AiProviderMetrics",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ProviderId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RefreshedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviderMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiProviderMetrics_AiProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "AiProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderMetrics_ProviderId_Kind",
                table: "AiProviderMetrics",
                columns: new[] { "ProviderId", "Kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiProviderMetrics");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "AiProviders");

            migrationBuilder.DropColumn(
                name: "LastMetricsError",
                table: "AiProviders");

            migrationBuilder.DropColumn(
                name: "LastMetricsRefreshUtc",
                table: "AiProviders");

            migrationBuilder.DropColumn(
                name: "ManagementApiKey",
                table: "AiProviders");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "AiProviders");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "AiProviders");

            migrationBuilder.DropColumn(
                name: "ActiveText",
                table: "AiProviderModels");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "AiProviderModels");

            migrationBuilder.DropColumn(
                name: "Endpoint",
                table: "AiProviderModels");

            migrationBuilder.DropColumn(
                name: "Repository",
                table: "AiProviderModels");
        }
    }
}
