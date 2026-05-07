using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRp.Migrations
{
    /// <inheritdoc />
    public partial class AddImageAvatarCrop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AvatarFocusXPercent",
                table: "ImageAssets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AvatarFocusYPercent",
                table: "ImageAssets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AvatarZoomPercent",
                table: "ImageAssets",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarFocusXPercent",
                table: "ImageAssets");

            migrationBuilder.DropColumn(
                name: "AvatarFocusYPercent",
                table: "ImageAssets");

            migrationBuilder.DropColumn(
                name: "AvatarZoomPercent",
                table: "ImageAssets");
        }
    }
}
