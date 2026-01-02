using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pivot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBrowserSchemaExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Rating column for user ratings (0-5)
            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "Assets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Add DominantColor column for image placeholder backgrounds
            migrationBuilder.AddColumn<string>(
                name: "DominantColor",
                table: "Assets",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            // Create performance indexes for scalable browser
            migrationBuilder.CreateIndex(
                name: "IX_Asset_Kind_Date",
                table: "Assets",
                columns: new[] { "Kind", "LastModifiedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Asset_AspectRatio",
                table: "Assets",
                column: "AspectRatio");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_Rating",
                table: "Assets",
                column: "Rating");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Asset_Rating",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Asset_AspectRatio",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Asset_Kind_Date",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DominantColor",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Assets");
        }
    }
}
