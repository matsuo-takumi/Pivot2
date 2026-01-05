using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pivot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSortOrderToAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Assets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Asset_SortOrder",
                table: "Assets",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Asset_SortOrder",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Assets");
        }
    }
}
