using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pivot.Models;

namespace Pivot.Services.Engines
{
    /// <summary>
    /// Engine for processing code/script files (.cs, .py, .js, .json, .xml, etc.)
    /// Handles metadata extraction and content indexing for code assets.
    /// </summary>
    public class CodeEngine : IAssetEngine
    {
        public int Priority => 10; // Lower priority than ImageEngine (20)

        public string[] SupportedExtensions => new[]
        {
            // C# and .NET
            ".cs", ".csx", ".vb", ".fs", ".fsx",
            // Web
            ".js", ".ts", ".jsx", ".tsx", ".html", ".htm", ".css", ".scss", ".sass", ".less",
            // Python
            ".py", ".pyw", ".pyx",
            // Data/Config
            ".json", ".xml", ".yaml", ".yml", ".toml", ".ini", ".cfg",
            // Shell/Script
            ".sh", ".bash", ".bat", ".cmd", ".ps1",
            // Other languages
            ".cpp", ".c", ".h", ".hpp", ".java", ".kt", ".go", ".rs", ".swift",
            ".rb", ".php", ".lua", ".sql", ".r",
            // Markup/Doc
            ".md", ".markdown", ".txt", ".log"
        };

        public async Task ProcessFileAsync(string filePath, AssetEntity asset, CancellationToken ct = default)
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                throw new FileNotFoundException("File not found", filePath);

            // Update basic file metadata
            asset.FilePath = filePath;
            asset.FileName = fileInfo.Name;
            asset.Directory = fileInfo.DirectoryName ?? string.Empty;
            asset.Extension = fileInfo.Extension.ToLowerInvariant();
            asset.FileSize = fileInfo.Length;
            asset.LastModifiedUtc = fileInfo.LastWriteTimeUtc;
            asset.Kind = AssetKind.Script; // Mark as Script/Code
            
            // Detect language from extension
            asset.Language = DetectLanguage(asset.Extension);

            // Read and index content (for search)
            try
            {
                // Limit content indexing to reasonable file sizes (< 1MB)
                if (fileInfo.Length < 1024 * 1024)
                {
                    var content = await File.ReadAllTextAsync(filePath, ct);
                    asset.ContentIndex = content; // Store for full-text search
                }
                else
                {
                    asset.ContentIndex = $"[File too large: {fileInfo.Length} bytes]";
                }
            }
            catch
            {
                asset.ContentIndex = "[Error reading file]";
            }

            asset.UpdatedAt = DateTime.UtcNow;
            if (asset.Id == 0)
            {
                asset.CreatedAt = DateTime.UtcNow;
            }
        }

        public bool IsUpToDate(AssetEntity entity)
        {
            // Code files don't have thumbnails, so just check file modification time
            var fileInfo = new FileInfo(entity.FilePath);
            if (!fileInfo.Exists) return false;

            return entity.LastModifiedUtc >= fileInfo.LastWriteTimeUtc;
        }

        private string DetectLanguage(string extension)
        {
            return extension switch
            {
                ".cs" or ".csx" => "C#",
                ".vb" => "Visual Basic",
                ".fs" or ".fsx" => "F#",
                ".js" or ".jsx" => "JavaScript",
                ".ts" or ".tsx" => "TypeScript",
                ".py" or ".pyw" or ".pyx" => "Python",
                ".cpp" or ".cc" or ".cxx" => "C++",
                ".c" => "C",
                ".h" or ".hpp" => "C/C++ Header",
                ".java" => "Java",
                ".kt" => "Kotlin",
                ".go" => "Go",
                ".rs" => "Rust",
                ".swift" => "Swift",
                ".rb" => "Ruby",
                ".php" => "PHP",
                ".lua" => "Lua",
                ".sql" => "SQL",
                ".r" => "R",
                ".html" or ".htm" => "HTML",
                ".css" => "CSS",
                ".scss" or ".sass" => "SCSS/SASS",
                ".less" => "LESS",
                ".json" => "JSON",
                ".xml" => "XML",
                ".yaml" or ".yml" => "YAML",
                ".toml" => "TOML",
                ".md" or ".markdown" => "Markdown",
                ".sh" or ".bash" => "Bash",
                ".bat" or ".cmd" => "Batch",
                ".ps1" => "PowerShell",
                _ => "Text"
            };
        }
    }
}
