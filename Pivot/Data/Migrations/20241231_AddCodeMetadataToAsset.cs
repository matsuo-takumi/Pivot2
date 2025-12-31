using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pivot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeMetadataToAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Assets",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tool",
                table: "Assets",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentIndex",
                table: "Assets",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Tool",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ContentIndex",
                table: "Assets");
        }
    }
}
