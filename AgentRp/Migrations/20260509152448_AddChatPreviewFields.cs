using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRp.Migrations
{
    /// <inheritdoc />
    public partial class AddChatPreviewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveLocationJson",
                table: "Chats",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "LastGeneratedTurnNumber",
                table: "Chats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMessageUtc",
                table: "Chats",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SceneCharactersJson",
                table: "Chats",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_LastMessageUtc",
                table: "Chats",
                column: "LastMessageUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Chats_LastMessageUtc",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "ActiveLocationJson",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "LastGeneratedTurnNumber",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "LastMessageUtc",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "SceneCharactersJson",
                table: "Chats");
        }
    }
}
