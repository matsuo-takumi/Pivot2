using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pivot.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Directory = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    Extension = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    AspectRatio = table.Column<double>(type: "REAL", nullable: true),
                    ThumbnailPath = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    ThumbnailGeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UserTagsJson = table.Column<string>(type: "TEXT", nullable: true),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Directory",
                table: "Assets",
                column: "Directory");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_FilePath",
                table: "Assets",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Hash",
                table: "Assets",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Kind",
                table: "Assets",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_LastModifiedUtc",
                table: "Assets",
                column: "LastModifiedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assets");
        }
    }
}
