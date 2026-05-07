using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRp.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceMessageStreaming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "VoiceIdsJson",
                table: "SpeechAssets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedUtc",
                table: "SpeechAssets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "SpeechAssets",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InputsJson",
                table: "SpeechAssets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedUtc",
                table: "SpeechAssets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "SpeechAssets",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Pending");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedUtc",
                table: "SpeechAssets");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "SpeechAssets");

            migrationBuilder.DropColumn(
                name: "InputsJson",
                table: "SpeechAssets");

            migrationBuilder.DropColumn(
                name: "StartedUtc",
                table: "SpeechAssets");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SpeechAssets");

            migrationBuilder.AlterColumn<string>(
                name: "VoiceIdsJson",
                table: "SpeechAssets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "{}");
        }
    }
}
