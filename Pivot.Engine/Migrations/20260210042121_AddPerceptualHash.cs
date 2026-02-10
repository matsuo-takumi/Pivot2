using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pivot.Engine.Migrations
{
    /// <inheritdoc />
    public partial class AddPerceptualHash : Migration
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
                    Language = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Tool = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ContentIndex = table.Column<string>(type: "TEXT", nullable: true),
                    ThumbnailPath = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    ThumbnailGeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UserTagsJson = table.Column<string>(type: "TEXT", nullable: true),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    DominantColor = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    PerceptualHash = table.Column<ulong>(type: "INTEGER", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmartFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IconGlyph = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CriteriaJson = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartFolders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssetColors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    R = table.Column<byte>(type: "INTEGER", nullable: false),
                    G = table.Column<byte>(type: "INTEGER", nullable: false),
                    B = table.Column<byte>(type: "INTEGER", nullable: false),
                    Ratio = table.Column<float>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetColors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetColors_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetColors_AssetId",
                table: "AssetColors",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_AspectRatio",
                table: "Assets",
                column: "AspectRatio");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_Kind_Date",
                table: "Assets",
                columns: new[] { "Kind", "LastModifiedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Asset_PerceptualHash",
                table: "Assets",
                column: "PerceptualHash");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_Rating",
                table: "Assets",
                column: "Rating");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_SortOrder",
                table: "Assets",
                column: "SortOrder");

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
                name: "AssetColors");

            migrationBuilder.DropTable(
                name: "SmartFolders");

            migrationBuilder.DropTable(
                name: "Assets");
        }
    }
}
