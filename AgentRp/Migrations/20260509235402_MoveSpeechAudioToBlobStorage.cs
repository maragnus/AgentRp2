using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRp.Migrations
{
    /// <inheritdoc />
    public partial class MoveSpeechAudioToBlobStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [SpeechAssets]");
            migrationBuilder.Sql("UPDATE [TranscriptTurns] SET [SpeechJson] = N'' WHERE [SpeechJson] <> N''");
            migrationBuilder.Sql("UPDATE [TranscriptSnapshots] SET [SpeechJson] = N'' WHERE [SpeechJson] <> N''");

            migrationBuilder.DropColumn(
                name: "Bytes",
                table: "SpeechAssets");

            migrationBuilder.AddColumn<string>(
                name: "BlobName",
                table: "SpeechAssets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "StoredByteLength",
                table: "SpeechAssets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlobName",
                table: "SpeechAssets");

            migrationBuilder.DropColumn(
                name: "StoredByteLength",
                table: "SpeechAssets");

            migrationBuilder.AddColumn<byte[]>(
                name: "Bytes",
                table: "SpeechAssets",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
