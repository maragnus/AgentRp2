using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRp.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceModelsAndChatModelSelections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveModelSelectionsJson",
                table: "ChatDocuments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{\"values\":{}}");

            migrationBuilder.AddColumn<string>(
                name: "LastVoiceRefreshError",
                table: "AiProviderModels",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastVoiceRefreshUtc",
                table: "AiProviderModels",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RolesJson",
                table: "AiProviderModels",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "VoicesJson",
                table: "AiProviderModels",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.Sql(
                """
                UPDATE AiProviderModels
                SET RolesJson = CASE
                    WHEN [Text] = 1 AND [Image] = 1 THEN '["Chat","Image"]'
                    WHEN [Text] = 1 THEN '["Chat"]'
                    WHEN [Image] = 1 THEN '["Image"]'
                    ELSE '[]'
                END,
                    VoicesJson = '[]';

                DECLARE @ProviderId nvarchar(80);
                DECLARE @ModelId nvarchar(500);

                SELECT TOP(1)
                    @ProviderId = ProviderId,
                    @ModelId = Id
                FROM AiProviderModels
                WHERE ActiveText = 1 AND [Text] = 1
                ORDER BY ProviderId, SortOrder;

                IF @ProviderId IS NOT NULL AND @ModelId IS NOT NULL
                BEGIN
                    UPDATE ChatDocuments
                    SET ActiveModelSelectionsJson = CONCAT(
                        '{"values":{"Chat":{"providerId":"',
                        STRING_ESCAPE(@ProviderId, 'json'),
                        '","modelId":"',
                        STRING_ESCAPE(@ModelId, 'json'),
                        '"}}}');
                END
                """);

            migrationBuilder.DropColumn(
                name: "ActiveText",
                table: "AiProviderModels");

            migrationBuilder.DropColumn(
                name: "Image",
                table: "AiProviderModels");

            migrationBuilder.DropColumn(
                name: "Text",
                table: "AiProviderModels");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ActiveText",
                table: "AiProviderModels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Image",
                table: "AiProviderModels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Text",
                table: "AiProviderModels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE AiProviderModels
                SET [Text] = CASE WHEN RolesJson LIKE '%"Chat"%' THEN 1 ELSE 0 END,
                    [Image] = CASE WHEN RolesJson LIKE '%"Image"%' THEN 1 ELSE 0 END;

                DECLARE @ProviderId nvarchar(80);
                DECLARE @ModelId nvarchar(500);

                SELECT TOP(1)
                    @ProviderId = JSON_VALUE(ActiveModelSelectionsJson, '$.values.Chat.providerId'),
                    @ModelId = JSON_VALUE(ActiveModelSelectionsJson, '$.values.Chat.modelId')
                FROM ChatDocuments
                WHERE JSON_VALUE(ActiveModelSelectionsJson, '$.values.Chat.providerId') IS NOT NULL
                  AND JSON_VALUE(ActiveModelSelectionsJson, '$.values.Chat.modelId') IS NOT NULL;

                IF @ProviderId IS NOT NULL AND @ModelId IS NOT NULL
                BEGIN
                    UPDATE AiProviderModels
                    SET ActiveText = 1
                    WHERE ProviderId = @ProviderId AND Id = @ModelId;
                END
                """);

            migrationBuilder.DropColumn(
                name: "ActiveModelSelectionsJson",
                table: "ChatDocuments");

            migrationBuilder.DropColumn(
                name: "LastVoiceRefreshError",
                table: "AiProviderModels");

            migrationBuilder.DropColumn(
                name: "LastVoiceRefreshUtc",
                table: "AiProviderModels");

            migrationBuilder.DropColumn(
                name: "RolesJson",
                table: "AiProviderModels");

            migrationBuilder.DropColumn(
                name: "VoicesJson",
                table: "AiProviderModels");
        }
    }
}
