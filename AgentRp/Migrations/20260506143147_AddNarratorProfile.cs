using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRp.Migrations
{
    /// <inheritdoc />
    public partial class AddNarratorProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NarratorProfileJson",
                table: "ChatDocuments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NarratorProfileJson",
                table: "ChatDocuments");
        }
    }
}
