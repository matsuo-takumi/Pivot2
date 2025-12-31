using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Pivot.Models;
using Pivot.Repositories;

namespace Pivot.Services
{
    public class LegacyRescueService
    {
        private readonly ILogger<LegacyRescueService> _logger;
        private readonly IAssetRepository _assetRepository;
        private const string DbName = "codehub.db";
        private const string MigrationMarker = "codehub_migrated.marker";

        public LegacyRescueService(ILogger<LegacyRescueService> logger, IAssetRepository assetRepository)
        {
            _logger = logger;
            _assetRepository = assetRepository;
        }

        /// <summary>
        /// Migrates CodeFiles from legacy codehub.db to AssetEntity in pivot.db.
        /// Only runs once per installation (checks for migration marker file).
        /// </summary>
        public async Task MigrateToAssetsAsync()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var pivotFolder = Path.Combine(localAppData, "Pivot");
            var dbPath = Path.Combine(pivotFolder, DbName);
            var markerPath = Path.Combine(pivotFolder, MigrationMarker);

            // Skip if already migrated
            if (File.Exists(markerPath))
            {
                _logger.LogInformation("LegacyRescueService: Migration already completed (marker exists). Skipping.");
                return;
            }

            if (!File.Exists(dbPath))
            {
                _logger.LogInformation("LegacyRescueService: No legacy codehub.db found. Skipping migration.");
                // Create marker anyway so we don't check again
                await File.WriteAllTextAsync(markerPath, $"No legacy DB found. Skipped at {DateTime.UtcNow:O}");
                return;
            }

            _logger.LogInformation("LegacyRescueService: Found legacy codehub.db. Starting migration to pivot.db...");

            int migratedCount = 0;
            int errorCount = 0;

            try
            {
                using var connection = new SqliteConnection($"Data Source={dbPath}");
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, FilePath, Title, Content, Language, Tool, Tags, Updated, IsDeleted, DeletedAt 
                    FROM CodeFiles";

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    try
                    {
                        var filePath = reader["FilePath"]?.ToString() ?? "";
                        var title = reader["Title"]?.ToString() ?? "Untitled";
                        var content = reader["Content"]?.ToString() ?? "";
                        var language = reader["Language"]?.ToString() ?? "";
                        var tool = reader["Tool"]?.ToString() ?? "";
                        var tagsCsv = reader["Tags"]?.ToString() ?? "";
                        var updatedStr = reader["Updated"]?.ToString();
                        var isDeletedVal = reader["IsDeleted"];
                        var deletedAtStr = reader["DeletedAt"]?.ToString();

                        // Skip if filePath is empty (DB-only snippet with no file)
                        if (string.IsNullOrWhiteSpace(filePath))
                        {
                            // Create a virtual file path for DB-only snippets
                            var safeTitle = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
                            if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = Guid.NewGuid().ToString("N")[..8];
                            var ext = MapLanguageToExtension(language);
                            filePath = Path.Combine(pivotFolder, "MigratedSnippets", $"{safeTitle}.{ext}");
                            
                            // Create the file with content
                            var snippetDir = Path.GetDirectoryName(filePath);
                            if (!string.IsNullOrEmpty(snippetDir)) Directory.CreateDirectory(snippetDir);
                            await File.WriteAllTextAsync(filePath, content);
                        }

                        // Check if already exists in pivot.db
                        var existing = await _assetRepository.GetByPathAsync(filePath);
                        if (existing != null)
                        {
                            _logger.LogDebug("LegacyRescueService: Asset already exists at {Path}. Skipping.", filePath);
                            continue;
                        }

                        // Parse dates
                        DateTime.TryParse(updatedStr, out var updated);
                        DateTime? deletedAt = null;
                        if (DateTime.TryParse(deletedAtStr, out var parsedDeletedAt))
                        {
                            deletedAt = parsedDeletedAt;
                        }
                        bool isDeleted = isDeletedVal != null && isDeletedVal != DBNull.Value && Convert.ToInt32(isDeletedVal) != 0;

                        // Convert tags from CSV to JSON array
                        var tagsJson = ConvertCsvToJsonArray(tagsCsv);

                        // Create AssetEntity
                        var asset = new AssetEntity
                        {
                            FilePath = filePath,
                            FileName = title,
                            Directory = Path.GetDirectoryName(filePath) ?? "",
                            Extension = Path.GetExtension(filePath).TrimStart('.'),
                            FileSize = File.Exists(filePath) ? new FileInfo(filePath).Length : content.Length,
                            LastModifiedUtc = updated == default ? DateTime.UtcNow : updated,
                            Kind = AssetKind.Code,
                            Language = language,
                            Tool = tool,
                            ContentIndex = content,
                            UserTagsJson = tagsJson,
                            IsDeleted = isDeleted,
                            DeletedAt = deletedAt,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = updated == default ? DateTime.UtcNow : updated
                        };

                        await _assetRepository.UpsertAsync(asset);
                        migratedCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogWarning(ex, "LegacyRescueService: Error migrating a CodeFile.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LegacyRescueService: Critical error during migration.");
            }

            // Create marker file
            var markerContent = $"Migration completed at {DateTime.UtcNow:O}\nMigrated: {migratedCount}\nErrors: {errorCount}";
            await File.WriteAllTextAsync(markerPath, markerContent);

            // Backup the old db
            if (migratedCount > 0 || errorCount == 0)
            {
                var backupPath = Path.Combine(pivotFolder, "Backup", $"codehub_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                var backupDir = Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(backupDir)) Directory.CreateDirectory(backupDir);
                File.Move(dbPath, backupPath);
                _logger.LogInformation("LegacyRescueService: Moved legacy DB to {BackupPath}", backupPath);
            }

            _logger.LogInformation("LegacyRescueService: Migration complete. Migrated: {Count}, Errors: {Errors}", migratedCount, errorCount);
        }

        private string ConvertCsvToJsonArray(string tagsCsv)
        {
            if (string.IsNullOrWhiteSpace(tagsCsv)) return "[]";
            var parts = tagsCsv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim();
            }
            return JsonSerializer.Serialize(parts);
        }

        private string MapLanguageToExtension(string lang)
        {
            return lang.ToLowerInvariant() switch
            {
                "c#" => "cs",
                "csharp" => "cs",
                "python" => "py",
                "javascript" => "js",
                "typescript" => "ts",
                "html" => "html",
                "css" => "css",
                "sql" => "sql",
                "json" => "json",
                "xml" => "xml",
                "markdown" => "md",
                "md" => "md",
                "cpp" => "cpp",
                "c++" => "cpp",
                "c" => "c",
                "hlsl" => "hlsl",
                "glsl" => "glsl",
                "vex" => "vfl",
                "hscript" => "txt",
                _ => "txt"
            };
        }
    }
}
