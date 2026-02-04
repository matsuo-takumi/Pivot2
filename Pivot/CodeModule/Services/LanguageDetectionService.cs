using System;
using System.IO;
using System.Linq;

namespace Pivot.CodeModule.Services
{
    /// <summary>
    /// Service for detecting programming language from code content or file extension.
    /// </summary>
    public class LanguageDetectionService
    {
        /// <summary>
        /// Detects programming language from code content using heuristic analysis.
        /// </summary>
        /// <param name="code">The code content to analyze.</param>
        /// <returns>Detected language identifier (e.g., "python", "csharp", "javascript").</returns>
        public string DetectFromContent(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return "text";

            // Normalize for analysis
            var normalized = code.Trim();

            // Python: def, import, class with colon syntax
            if (normalized.Contains("def ") || 
                (normalized.Contains("import ") && normalized.Contains(":")))
                return "python";

            // JavaScript/TypeScript: arrow functions, const/let, function keyword
            if (normalized.Contains("=>") || 
                normalized.Contains("const ") || 
                normalized.Contains("let ") ||
                normalized.Contains("function "))
                return "javascript";

            // C#: namespace, public class, using System
            if (normalized.Contains("namespace ") || 
                normalized.Contains("public class ") ||
                normalized.Contains("using System"))
                return "csharp";

            // C/C++: #include, std::
            if (normalized.Contains("#include") || 
                normalized.Contains("std::"))
                return "cpp";

            // SQL: SELECT, FROM, WHERE (case insensitive)
            var upper = normalized.ToUpperInvariant();
            if (upper.Contains("SELECT ") && 
                (upper.Contains("FROM ") || upper.Contains("WHERE ")))
                return "sql";

            // HTML: tags
            if (normalized.Contains("<html") || 
                normalized.Contains("<!DOCTYPE") ||
                (normalized.Contains("<div") && normalized.Contains("</div>")))
                return "html";

            // CSS: selectors and properties
            if (normalized.Contains("{") && 
                normalized.Contains("}") && 
                (normalized.Contains("color:") || normalized.Contains("margin:") || normalized.Contains("padding:")))
                return "css";

            // JSON: starts with { or [
            if ((normalized.StartsWith("{") && normalized.EndsWith("}")) ||
                (normalized.StartsWith("[") && normalized.EndsWith("]")))
            {
                try
                {
                    // Simple validation
                    if (normalized.Contains("\":") || normalized.Contains("\","))
                        return "json";
                }
                catch { }
            }

            // XML: starts with <
            if (normalized.StartsWith("<") && normalized.Contains("</"))
                return "xml";

            // Markdown: headers, lists
            if (normalized.Contains("# ") || 
                normalized.Contains("## ") ||
                (normalized.Contains("- ") && normalized.Contains("\n")))
                return "markdown";

            // Default
            return "text";
        }

        /// <summary>
        /// Detects programming language from file extension.
        /// </summary>
        /// <param name="filename">The filename or path.</param>
        /// <returns>Detected language identifier, or "text" if unknown.</returns>
        public string DetectFromExtension(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return "text";

            var extension = Path.GetExtension(filename).ToLowerInvariant();

            return extension switch
            {
                ".py" => "python",
                ".js" => "javascript",
                ".ts" => "typescript",
                ".jsx" => "javascript",
                ".tsx" => "typescript",
                ".cs" => "csharp",
                ".cpp" or ".cc" or ".cxx" or ".c++" => "cpp",
                ".c" => "c",
                ".h" or ".hpp" => "cpp",
                ".java" => "java",
                ".kt" => "kotlin",
                ".swift" => "swift",
                ".go" => "go",
                ".rs" => "rust",
                ".rb" => "ruby",
                ".php" => "php",
                ".sql" => "sql",
                ".html" or ".htm" => "html",
                ".css" => "css",
                ".scss" or ".sass" => "scss",
                ".json" => "json",
                ".xml" => "xml",
                ".yaml" or ".yml" => "yaml",
                ".md" or ".markdown" => "markdown",
                ".sh" or ".bash" => "bash",
                ".ps1" => "powershell",
                ".bat" or ".cmd" => "batch",
                ".r" => "r",
                ".m" => "matlab",
                ".lua" => "lua",
                ".pl" => "perl",
                ".vb" => "vb",
                ".fs" or ".fsx" => "fsharp",
                ".dart" => "dart",
                ".scala" => "scala",
                ".groovy" => "groovy",
                _ => "text"
            };
        }

        /// <summary>
        /// Detects language using both filename and content.
        /// Filename takes precedence if available.
        /// </summary>
        /// <param name="filename">Optional filename.</param>
        /// <param name="code">Code content.</param>
        /// <returns>Detected language identifier.</returns>
        public string Detect(string? filename, string code)
        {
            // Try filename first
            if (!string.IsNullOrWhiteSpace(filename))
            {
                var fromExtension = DetectFromExtension(filename);
                if (fromExtension != "text")
                    return fromExtension;
            }

            // Fall back to content analysis
            return DetectFromContent(code);
        }
    }
}
