using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Pivot.CodeModule.Models;
using Pivot.Services;

namespace Pivot.CodeModule.Services
{
    // Minimal filesystem-backed repository used as a fallback when SQLite/EF isn't available.
    public class FileCodeRepository : ICodeRepository
    {
        private readonly SettingsService _settings;
        private readonly object _fileSaveLock = new object();

        public FileCodeRepository(SettingsService settings)
        {
            _settings = settings;
        }

        public IEnumerable<CodeFile> GetAll()
        {
            var list = new List<CodeFile>();
            try
            {
                var userSettings = _settings.GetUserSettings();
                var dirs = userSettings?.CodeDirectories ?? new List<string>();
                var allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".json", ".cs", ".py", ".md", ".txt" };

                foreach (var dir in dirs)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;
                        var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                                             .Where(f => allowedExts.Contains(Path.GetExtension(f) ?? string.Empty));
                        foreach (var file in files)
                        {
                            try
                            {
                                var ext = Path.GetExtension(file);
                                CodeFile? parsed = null;
                                if (string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase))
                                {
                                    var txt = File.ReadAllText(file);
                                    try { parsed = JsonSerializer.Deserialize<CodeFile>(txt); } catch { parsed = null; }
                                }
                                if (parsed == null)
                                {
                                    var content = File.ReadAllText(file);
                                    parsed = new CodeFile
                                    {
                                        Title = Path.GetFileNameWithoutExtension(file),
                                        Content = content,
                                        Language = ext.TrimStart('.').ToUpperInvariant(),
                                        Tags = string.Empty,
                                        Updated = File.GetLastWriteTimeUtc(file),
                                    };
                                    parsed.Id = CreateDeterministicGuid(file);
                                }
                                if (!list.Any(l => l.Id == parsed.Id) && (parsed.IsDeleted == false)) list.Add(parsed);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                // Also include exported snippets in the configured export directory
                try
                {
                    var exportDir = _settings.GetExportOutputDirectory();
                if (!string.IsNullOrWhiteSpace(exportDir) && Directory.Exists(exportDir))
                    {
                        var files = Directory.EnumerateFiles(exportDir, "*.*", SearchOption.TopDirectoryOnly)
                                             .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
                        foreach (var file in files)
                        {
                            try
                            {
                                if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                                {
                                    var txt = File.ReadAllText(file);
                                    var parsed = JsonSerializer.Deserialize<CodeFile>(txt);
                                    if (parsed != null && !list.Any(l => l.Id == parsed.Id) && (parsed.IsDeleted == false)) list.Add(parsed);
                                }
                                else
                                {
                                    // For markdown, attempt to parse YAML front matter minimal; fallback to raw content
                                    var txt = File.ReadAllText(file);
                                    var title = Path.GetFileNameWithoutExtension(file);
                                    var cf = new CodeFile { Title = title, Content = txt, Updated = File.GetLastWriteTimeUtc(file) };
                                    if (!list.Any(l => l.Id == cf.Id)) list.Add(cf);
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            catch { }

            return list.OrderByDescending(c => c.Updated);
        }

        public IEnumerable<CodeFile> Search(string query)
        {
            return GetAll().Where(c => (c.Title ?? string.Empty).Contains(query ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                                     || (c.Content ?? string.Empty).Contains(query ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<CodeFile> Filter(string language, string tool, string tag)
        {
            return GetAll().Where(c =>
                (string.IsNullOrEmpty(language) || c.Language == language) &&
                (string.IsNullOrEmpty(tool) || c.Tool == tool) &&
                (string.IsNullOrEmpty(tag) || (c.Tags ?? string.Empty).Contains(tag)));
        }

        public void Save(CodeFile file)
        {
            try
            {
                // Attempt to overwrite originating file in CodeDirectories
                var userSettings = _settings.GetUserSettings();
                var dirs = userSettings?.CodeDirectories ?? new List<string>();
                var allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".json", ".md", ".cs", ".py", ".txt" };
                var overwritten = false;
                foreach (var dir in dirs)
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
                                    if (string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var json = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true });
                                        File.WriteAllText(src, json);
                                    }
                                    else if (string.Equals(ext, ".md", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var tags = (file.Tags ?? string.Empty);
                                        var yaml = $"---\nid: {file.Id}\ntitle: \"{file.Title}\"\ntags: []\nupdated: \"{file.Updated:O}\"\n---\n";
                                        File.WriteAllText(src, yaml + (file.Content ?? string.Empty));
                                    }
                                    else
                                    {
                                        File.WriteAllText(src, file.Content ?? string.Empty);
                                    }
                                    overwritten = true;
                                    break;
                                }
                            }
                            catch { }
                        }
                        if (overwritten) break;
                    }
                    catch { }
                }

                if (!overwritten)
                {
                    // Export to configured directory
                    var exportDir = _settings.GetExportOutputDirectory();
                    if (string.IsNullOrWhiteSpace(exportDir))
                    {
                        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        exportDir = Path.Combine(docs, "Pivot", "CodeSnippets");
                    }
                    try
                    {
                        Directory.CreateDirectory(exportDir);
                        var outPath = Path.Combine(exportDir, file.Id.ToString() + ".json");
                        var json = JsonSerializer.Serialize(new
                        {
                            file.Id,
                            file.Title,
                            file.Language,
                            file.Tool,
                            file.Tags,
                            file.Content,
                            file.Updated
                        }, new JsonSerializerOptions { WriteIndented = true });
                        lock (_fileSaveLock) File.WriteAllText(outPath, json);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] FileCodeRepository.Save: export failed: {ex}");
                    }
                }
            }
            catch { }
        }

        public void Delete(Guid id)
        {
            try
            {
                // Mark deleted in any JSON export, or write a deleted JSON sentinel in export directory
                var exportDir = _settings.GetExportOutputDirectory();
                if (string.IsNullOrWhiteSpace(exportDir))
                {
                    var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    exportDir = Path.Combine(docs, "Pivot", "CodeSnippets");
                }
                try { if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir); } catch { }

                var path = Path.Combine(exportDir, id.ToString() + ".json");
                if (File.Exists(path))
                {
                    try
                    {
                        var txt = File.ReadAllText(path);
                        var parsed = JsonSerializer.Deserialize<CodeFile>(txt);
                        if (parsed != null)
                        {
                            parsed.IsDeleted = true;
                            parsed.DeletedAt = DateTime.UtcNow;
                            File.WriteAllText(path, JsonSerializer.Serialize(parsed, new JsonSerializerOptions { WriteIndented = true }));
                            return;
                        }
                    }
                    catch { }
                }

                // If no existing export, create a minimal deleted JSON to represent the trash state
                var deletedJson = JsonSerializer.Serialize(new CodeFile { Id = id, IsDeleted = true, DeletedAt = DateTime.UtcNow }, new JsonSerializerOptions { WriteIndented = true });
                try { File.WriteAllText(path, deletedJson); } catch { }
            }
            catch { }
        }

        public IEnumerable<CodeFile> GetAllDeleted()
        {
            var list = new List<CodeFile>();
            try
            {
                var userSettings = _settings.GetUserSettings();
                var dirs = userSettings?.CodeDirectories ?? new List<string>();
                var allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".json", ".cs", ".py", ".md", ".txt" };

                foreach (var dir in dirs)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;
                        var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                                             .Where(f => allowedExts.Contains(Path.GetExtension(f) ?? string.Empty));
                        foreach (var file in files)
                        {
                            try
                            {
                                var ext = Path.GetExtension(file);
                                if (string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase))
                                {
                                    var txt = File.ReadAllText(file);
                                    var parsed = JsonSerializer.Deserialize<CodeFile>(txt);
                                    if (parsed != null && parsed.IsDeleted) list.Add(parsed);
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                // Also include exported snippets in the configured export directory
                try
                {
                    var exportDir = _settings.GetExportOutputDirectory();
                    if (!string.IsNullOrWhiteSpace(exportDir) && Directory.Exists(exportDir))
                    {
                        var files = Directory.EnumerateFiles(exportDir, "*.*", SearchOption.TopDirectoryOnly)
                                             .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
                        foreach (var file in files)
                        {
                            try
                            {
                                if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                                {
                                    var txt = File.ReadAllText(file);
                                    var parsed = JsonSerializer.Deserialize<CodeFile>(txt);
                                    if (parsed != null && parsed.IsDeleted) list.Add(parsed);
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            catch { }

            // Purge items older than 30 days from export directory
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-30);
                var exportDir = _settings.GetExportOutputDirectory();
                if (!string.IsNullOrWhiteSpace(exportDir) && Directory.Exists(exportDir))
                {
                    var files = Directory.EnumerateFiles(exportDir, "*.json", SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        try
                        {
                            var txt = File.ReadAllText(file);
                            var parsed = JsonSerializer.Deserialize<CodeFile>(txt);
                            if (parsed != null && parsed.IsDeleted && parsed.DeletedAt.HasValue && parsed.DeletedAt.Value < cutoff)
                            {
                                try { File.Delete(file); } catch { }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return list.OrderByDescending(c => c.DeletedAt);
        }

        public void Restore(Guid id)
        {
            try
            {
                var exportDir = _settings.GetExportOutputDirectory();
                if (string.IsNullOrWhiteSpace(exportDir))
                {
                    var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    exportDir = Path.Combine(docs, "Pivot", "CodeSnippets");
                }
                var path = Path.Combine(exportDir, id.ToString() + ".json");
                if (File.Exists(path))
                {
                    try
                    {
                        var txt = File.ReadAllText(path);
                        var parsed = JsonSerializer.Deserialize<CodeFile>(txt);
                        if (parsed != null)
                        {
                            parsed.IsDeleted = false;
                            parsed.DeletedAt = null;
                            File.WriteAllText(path, JsonSerializer.Serialize(parsed, new JsonSerializerOptions { WriteIndented = true }));
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        public void AddTag(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                var filters = _settings.GetCodeFilters() ?? new List<Pivot.Models.CustomFilter>();
                if (filters.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))) return;
                filters.Add(new Pivot.Models.CustomFilter { Name = name });
                _settings.SetCodeFiltersAsync(filters).ConfigureAwait(false);
            }
            catch { }
        }

        public IEnumerable<Pivot.CodeModule.Models.CodeTag> GetAllTags()
        {
            try
            {
                var list = new List<Pivot.CodeModule.Models.CodeTag>();
                var filters = _settings.GetCodeFilters() ?? new List<Pivot.Models.CustomFilter>();
                int idx = 1;
                foreach (var f in filters.OrderBy(ff => ff.SortOrder).ThenBy(ff => ff.Name))
                {
                    list.Add(new Pivot.CodeModule.Models.CodeTag { Id = idx++, Name = f.Name ?? string.Empty });
                }
                return list;
            }
            catch { }
            return Enumerable.Empty<Pivot.CodeModule.Models.CodeTag>();
        }

        public string GetFilterNameById(Guid filterId) => _settings.GetFilterNameById(filterId);

        private static Guid CreateDeterministicGuid(string input)
        {
            try
            {
                using var md5 = System.Security.Cryptography.MD5.Create();
                var bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
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
    }
}


