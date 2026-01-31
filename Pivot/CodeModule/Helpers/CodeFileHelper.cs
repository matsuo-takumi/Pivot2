using System;
using System.IO;

namespace Pivot.CodeModule.Helpers
{
    /// <summary>
    /// Centralized helper for code file operations, language mappings, and path management.
    /// </summary>
    public static class CodeFileHelper
    {
        /// <summary>
        /// Gets the root directory for storing code snippets.
        /// </summary>
        public static string GetCodeDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                "Pivot", 
                "Code");
        }

        /// <summary>
        /// Maps a language identifier to its file extension.
        /// </summary>
        public static string GetExtensionForLanguage(string? language)
        {
            return language?.ToLowerInvariant() switch
            {
                "python" => ".py",
                "javascript" or "js" => ".js",
                "typescript" or "ts" => ".ts",
                "c#" or "csharp" => ".cs",
                "c++" or "cpp" => ".cpp",
                "c" => ".c",
                "go" => ".go",
                "rust" => ".rs",
                "sql" => ".sql",
                "shell" or "bash" => ".sh",
                "powershell" or "ps1" => ".ps1",
                "vex" => ".vex",
                "hlsl" => ".hlsl",
                "glsl" => ".glsl",
                "html" => ".html",
                "css" => ".css",
                "json" => ".json",
                "xml" => ".xml",
                "yaml" => ".yaml",
                "markdown" or "md" => ".md",
                "java" => ".java",
                "kotlin" => ".kt",
                "swift" => ".swift",
                "ruby" => ".rb",
                "php" => ".php",
                _ => ".txt"
            };
        }

        /// <summary>
        /// Maps a file extension to Editor language identifier.
        /// </summary>
        public static string GetLanguageFromExtension(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return "plaintext";

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".py" => "python",
                ".js" => "javascript",
                ".ts" => "typescript",
                ".cs" => "csharp",
                ".cpp" or ".cc" or ".cxx" => "cpp",
                ".c" => "c",
                ".h" or ".hpp" => "cpp",
                ".java" => "java",
                ".go" => "go",
                ".rs" => "rust",
                ".rb" => "ruby",
                ".php" => "php",
                ".swift" => "swift",
                ".kt" => "kotlin",
                ".sql" => "sql",
                ".html" or ".htm" => "html",
                ".css" => "css",
                ".scss" => "scss",
                ".json" => "json",
                ".xml" => "xml",
                ".yaml" or ".yml" => "yaml",
                ".md" => "markdown",
                ".sh" or ".bash" => "shell",
                ".ps1" => "powershell",
                ".bat" or ".cmd" => "bat",
                ".vex" => "cpp", // VEX is C-like
                _ => "plaintext"
            };
        }

        /// <summary>
        /// Sanitizes a filename by replacing invalid characters.
        /// </summary>
        public static string SanitizeFileName(string name, int maxLength = 50)
        {
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name.Length > maxLength ? name.Substring(0, maxLength) : name;
        }
    }
}
