using System;
using System.Collections.Generic;
using System.Linq;
using Pivot.CodeModule.Models;
using Pivot.Services;
using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Pivot.CodeModule.Services
{
    public class CodeRepository : ICodeRepository
    {
        private readonly SQLiteDbContext _context;
        private readonly SettingsService _settingsService;
        private readonly object _saveLock = new object();

        public CodeRepository(SQLiteDbContext context, SettingsService settingsService)
        {
            _context = context;
            _settingsService = settingsService;
        }

        public IEnumerable<CodeFile> GetAll()
        {
            // Start with persisted snippets from DB
            var dbList = _context.CodeFiles.OrderByDescending(c => c.Updated).ToList();

            // Try to augment with files found in user-configured code directories
            try
            {
                var userSettings = _settingsService?.GetUserSettings();
                var dirs = userSettings?.CodeDirectories ?? new List<string>();
                var allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".json", ".cs", ".py", ".md", ".txt" };

                foreach (var dir in dirs)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(dir)) continue;
                        if (!Directory.Exists(dir)) continue;
                        var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                                             .Where(f => allowedExts.Contains(Path.GetExtension(f)));
                        foreach (var file in files)
                        {
                            try
                            {
                                var ext = Path.GetExtension(file);
                                CodeFile? parsed = null;
                                if (string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Try deserialize exported snippet JSON into CodeFile
                                    var txt = File.ReadAllText(file);
                                    try
                                    {
                                        parsed = JsonSerializer.Deserialize<CodeFile>(txt);
                                    }
                                    catch
                                    {
                                        // Not matching schema; treat as raw content below
                                        parsed = null;
                                    }
                                }

                                if (parsed == null)
                                {
                                    // Create snippet from raw file content
                                    var content = File.ReadAllText(file);
                                    var title = Path.GetFileNameWithoutExtension(file);
                                    var lang = ext.TrimStart('.').ToUpperInvariant();
                                    parsed = new CodeFile
                                    {
                                        Title = title,
                                        Content = content,
                                        Language = lang,
                                        Tags = string.Empty,
                                        Updated = File.GetLastWriteTimeUtc(file),
                                    };
                                    // Deterministic Id from file path so same file yields same snippet Id across runs
                                    parsed.Id = CreateDeterministicGuid(file);
                                }

                                // Skip if DB already contains same Id
                                if (dbList.Any(d => d.Id == parsed.Id)) continue;

                                dbList.Add(parsed);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // Return combined list ordered by Updated desc
            return dbList.OrderByDescending(c => c.Updated);
        }

        private static Guid CreateDeterministicGuid(string input)
        {
            try
            {
                using var md5 = MD5.Create();
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
                if (bytes.Length >= 16)
                {
                    var guidBytes = new byte[16];
                    Array.Copy(bytes, guidBytes, 16);
                    return new Guid(guidBytes);
                }
            }
            catch { }
            return Guid.NewGuid();
        }

        public IEnumerable<CodeFile> Search(string query)
            => _context.CodeFiles
               .Where(c => c.Title.Contains(query) || c.Content.Contains(query) || c.Tags.Contains(query));

        public IEnumerable<CodeFile> Filter(string language, string tool, string tag)
            => _context.CodeFiles.Where(c =>
                (string.IsNullOrEmpty(language) || c.Language == language) &&
                (string.IsNullOrEmpty(tool) || c.Tool == tool) &&
                (string.IsNullOrEmpty(tag) || c.Tags.Contains(tag)));

        public void Save(CodeFile file)
        {
            if (_context.CodeFiles.Any(f => f.Id == file.Id))
                _context.CodeFiles.Update(file);
            else
                _context.CodeFiles.Add(file);

            try
            {
                lock (_saveLock)
                {
                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] CodeRepository.Save: SaveChanges failed: {ex}");
                // continue to attempt export even if DB save failed
            }

            // Export saved snippet to user-configured output directory (format configurable; default JSON)
            try
            {
                // Do not export empty content files to disk (they may be transient/new placeholders)
                if (string.IsNullOrWhiteSpace(file.Content))
                {
                    System.Diagnostics.Debug.WriteLine($"CodeRepository.Save: skipping export for empty content id={file.Id}");
                    return;
                }

                var exportDir = _settingsService?.GetExportOutputDirectory();
                System.Diagnostics.Debug.WriteLine($"CodeRepository.Save: exportDir='{exportDir}'");
                // Fallback directory if not configured or invalid
                if (string.IsNullOrWhiteSpace(exportDir))
                {
                    try
                    {
                        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        exportDir = System.IO.Path.Combine(docs, "Pivot", "CodeSnippets");
                    }
                    catch
                    {
                        // fallback to local app data
                        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        exportDir = System.IO.Path.Combine(local, "Pivot", "CodeSnippets");
                    }
                }

                try
                {
                    if (!System.IO.Directory.Exists(exportDir)) System.IO.Directory.CreateDirectory(exportDir);
                }
                catch { /* swallow */ }

                if (System.IO.Directory.Exists(exportDir))
                {
                    var format = _settingsService?.GetCodeExportFormat() ?? Pivot.Models.CodeExportFormat.Json;
                    System.Diagnostics.Debug.WriteLine($"CodeRepository.Save: format='{format}'");
                    var overwritten = false;
                    // Try to find an originating file in CodeDirectories and overwrite it if found
                    try
                    {
                        var userSettings = _settingsService?.GetUserSettings();
                        var dirs = userSettings?.CodeDirectories ?? new List<string>();
                        System.Diagnostics.Debug.WriteLine($"CodeRepository.Save: scanning CodeDirectories count={dirs?.Count ?? 0}");
                        var allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".json", ".cs", ".py", ".md", ".txt" };
                        foreach (var dir in dirs!)
                        {
                            try
                            {
                                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;
                                var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                                                     .Where(f => allowedExts.Contains(Path.GetExtension(f) ?? string.Empty));
                                foreach (var src in files)
                                {
                                    try
                                    {
                                                if (CreateDeterministicGuid(src) == file.Id)
                                        {
                                            var ext = Path.GetExtension(src);
                                                    System.Diagnostics.Debug.WriteLine($"CodeRepository.Save: found originating file='{src}' ext='{ext}'");
                                            if (string.Equals(ext, ".md", StringComparison.OrdinalIgnoreCase))
                                            {
                                                var tags = (file.Tags ?? string.Empty);
                                                var yaml = $"---\nid: {file.Id}\ntitle: \"{EscapeYaml(file.Title)}\"\ntags: [{EscapeYamlInlineList(tags)}]\nupdated: \"{file.Updated:O}\"\n---\n";
                                                var body = file.Content ?? string.Empty;
                                                System.IO.File.WriteAllText(src, yaml + body);
                                            }
                                            else if (string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase))
                                            {
                                                var json = System.Text.Json.JsonSerializer.Serialize(new
                                                {
                                                    file.Id,
                                                    file.Title,
                                                    file.Language,
                                                    file.Tool,
                                                    file.Tags,
                                                    file.Content,
                                                    file.Updated
                                                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                                                System.IO.File.WriteAllText(src, json);
                                            }
                                            else
                                            {
                                                // code file (.cs, .py, .txt, etc) - write raw content
                                                System.IO.File.WriteAllText(src, file.Content ?? string.Empty);
                                            }
                                            overwritten = true;
                                            System.Diagnostics.Debug.WriteLine($"CodeRepository.Save: overwritten originating file='{src}'");
                                            break;
                                        }
                                    }
                                    catch { }
                                }
                                if (overwritten) break;
                            }
                            catch { }
                        }
                    }
                    catch { }
                    System.Diagnostics.Debug.WriteLine($"CodeRepository.Save: overwritten={overwritten}");
                    switch (format)
                    {
                        case Pivot.Models.CodeExportFormat.Markdown:
                            {
                                if (!overwritten)
                                {
                                    var fileName = file.Id.ToString() + ".md";
                                    var outPath = System.IO.Path.Combine(exportDir, fileName);
                                    // Save with YAML front matter + content
                                    var tags = (file.Tags ?? string.Empty);
                                    var yaml = $"---\nid: {file.Id}\ntitle: \"{EscapeYaml(file.Title)}\"\ntags: [{EscapeYamlInlineList(tags)}]\nupdated: \"{file.Updated:O}\"\n---\n";
                                    var body = file.Content ?? string.Empty;
                                try
                                {
                                    System.IO.File.WriteAllText(outPath, yaml + body);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG] CodeRepository.Save: write md failed '{outPath}': {ex}");
                                }
                                    System.Diagnostics.Debug.WriteLine($"CodeRepository.Save: exported markdown to '{outPath}'");
                                }
                                break;
                            }
                        case Pivot.Models.CodeExportFormat.Json:
                        default:
                            {
                                if (!overwritten)
                                {
                                    var fileName = file.Id.ToString() + ".json"; // use Id to avoid collisions
                                    var outPath = System.IO.Path.Combine(exportDir, fileName);
                                    var json = System.Text.Json.JsonSerializer.Serialize(new
                                    {
                                        file.Id,
                                        file.Title,
                                        file.Language,
                                        file.Tool,
                                        file.Tags,
                                        file.Content,
                                        file.Updated
                                    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                                try
                                {
                                    System.IO.File.WriteAllText(outPath, json);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG] CodeRepository.Save: write json failed '{outPath}': {ex}");
                                }
                                    System.Diagnostics.Debug.WriteLine($"CodeRepository.Save: exported json to '{outPath}'");
                                }
                                break;
                            }
                    }
                }
            }
            catch { }
        }

        private static string EscapeYaml(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Replace("\"", "\\\"");
        }

        private static string EscapeYamlInlineList(string? tagsCsv)
        {
            if (string.IsNullOrEmpty(tagsCsv)) return string.Empty;
            var parts = tagsCsv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(s => s.Trim())
                               .Where(s => !string.IsNullOrWhiteSpace(s))
                               .Select(s => $"\"{EscapeYaml(s)}\"");
            return string.Join(", ", parts);
        }

        public void Delete(Guid id)
        {
            var item = _context.CodeFiles.Find(id);
            if (item != null)
            {
                _context.CodeFiles.Remove(item);
                _context.SaveChanges();
            }

            // Also attempt to delete any exported files for this snippet.
            try
            {
                var exportDir = _settingsService?.GetExportOutputDirectory();
                if (string.IsNullOrWhiteSpace(exportDir))
                {
                    try
                    {
                        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        exportDir = System.IO.Path.Combine(docs, "Pivot", "CodeSnippets");
                    }
                    catch
                    {
                        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        exportDir = System.IO.Path.Combine(local, "Pivot", "CodeSnippets");
                    }
                }

                if (!string.IsNullOrWhiteSpace(exportDir) && Directory.Exists(exportDir))
                {
                    var patterns = new[] { $"{id}.json", $"{id}.md", $"{id}.txt", $"{id}.py", $"{id}.cs" };
                    foreach (var pat in patterns)
                    {
                        try
                        {
                            var p = Path.Combine(exportDir, pat);
                            if (File.Exists(p))
                            {
                                File.Delete(p);
                                System.Diagnostics.Debug.WriteLine($"CodeRepository.Delete: removed exported file '{p}' for id={id}");
                            }
                        }
                        catch { }
                    }
                }

                // Also scan user configured CodeDirectories for originating files and delete them if their deterministic Id matches.
                try
                {
                    var userSettings = _settingsService?.GetUserSettings();
                    var dirs = userSettings?.CodeDirectories ?? new List<string>();
                    var allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".json", ".cs", ".py", ".md", ".txt" };
                    foreach (var dir in dirs)
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;
                            var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                                                 .Where(f => allowedExts.Contains(Path.GetExtension(f)));
                            foreach (var file in files)
                            {
                                try
                                {
                                    if (CreateDeterministicGuid(file) == id)
                                    {
                                        try { File.Delete(file); System.Diagnostics.Debug.WriteLine($"CodeRepository.Delete: removed originating file '{file}' for id={id}"); } catch { }
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            catch { }
        }

        public IEnumerable<Pivot.CodeModule.Models.CodeTag> GetAllTags()
        {
            return _context.Tags.OrderBy(t => t.Name).ToList();
        }

        public void AddTag(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            var trimmed = name.Trim();
            if (_context.Tags.Any(t => t.Name == trimmed)) return;
            _context.Tags.Add(new Pivot.CodeModule.Models.CodeTag { Name = trimmed });
            _context.SaveChanges();
        }

        public string GetFilterNameById(Guid filterId)
        {
            return _settingsService.GetFilterNameById(filterId);
        }
    }
}


