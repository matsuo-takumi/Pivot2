using System;
using System.Collections.Generic;
using System.Linq;
using Pivot.CodeModule.Models;
using Pivot.Services;
using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Data.Common;
using System.Data;
using Microsoft.EntityFrameworkCore;

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
            // Ensure DB schema contains soft-delete columns so queries won't fail on older DBs.
            try { EnsureCodeFilesSchema(); } catch { }
        }

        private void EnsureCodeFilesSchema()
        {
            DbConnection? conn = null;
            try
            {
                conn = _context.Database.GetDbConnection();
                if (conn == null) return;
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('CodeFiles');";
                using var reader = cmd.ExecuteReader();
                var hasIsDeleted = false;
                var hasDeletedAt = false;
                while (reader.Read())
                {
                    try
                    {
                        var name = reader["name"]?.ToString() ?? string.Empty;
                        if (string.Equals(name, "IsDeleted", StringComparison.OrdinalIgnoreCase)) hasIsDeleted = true;
                        if (string.Equals(name, "DeletedAt", StringComparison.OrdinalIgnoreCase)) hasDeletedAt = true;
                    }
                    catch { }
                }
                reader.Close();

                if (!hasIsDeleted)
                {
                    using var a = conn.CreateCommand();
                    a.CommandText = "ALTER TABLE CodeFiles ADD COLUMN IsDeleted INTEGER DEFAULT 0;";
                    a.ExecuteNonQuery();
                }
                if (!hasDeletedAt)
                {
                    using var b = conn.CreateCommand();
                    b.CommandText = "ALTER TABLE CodeFiles ADD COLUMN DeletedAt TEXT;";
                    b.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnsureCodeFilesSchema: failed - {ex}");
            }
            finally
            {
                try { if (conn != null && conn.State == ConnectionState.Open) conn.Close(); } catch { }
            }
        }

        public IEnumerable<CodeFile> GetAll()
        {
            // Start with persisted snippets from DB (exclude soft-deleted)
            var dbList = _context.CodeFiles.Where(c => !c.IsDeleted).OrderByDescending(c => c.Updated).ToList();

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
               .Where(c => !c.IsDeleted && (c.Title.Contains(query) || c.Content.Contains(query) || c.Tags.Contains(query)));

        public IEnumerable<CodeFile> Filter(string language, string tool, string tag)
            => _context.CodeFiles.Where(c => !c.IsDeleted &&
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
            // Soft-delete: mark as deleted and set timestamp. This moves the snippet to Trash for 30 days.
            var item = _context.CodeFiles.Find(id);
            if (item != null)
            {
                item.IsDeleted = true;
                item.DeletedAt = DateTime.UtcNow;
                try
                {
                    lock (_saveLock) { _context.Update(item); _context.SaveChanges(); }
                }
                catch { }
            }

            try
            {
                PermanentlyRemoveFilesForId(id);
            }
            catch { }
        }

        // Return deleted items that are still within the 30-day restore window.
        public IEnumerable<CodeFile> GetAllDeleted()
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-30);
                // Purge items older than 30 days (permanent delete)
                var toPurge = _context.CodeFiles.Where(c => c.IsDeleted && c.DeletedAt.HasValue && c.DeletedAt.Value < cutoff).ToList();
                foreach (var p in toPurge)
                {
                    try
                    {
                        // attempt to remove exported/originating files
                        PermanentlyRemoveFilesForId(p.Id);
                    }
                    catch { }
                    try { _context.CodeFiles.Remove(p); } catch { }
                }
                if (toPurge.Any())
                {
                    try { lock (_saveLock) { _context.SaveChanges(); } } catch { }
                }

                return _context.CodeFiles.Where(c => c.IsDeleted && (!c.DeletedAt.HasValue || c.DeletedAt.Value >= cutoff)).OrderByDescending(c => c.DeletedAt).ToList();
            }
            catch
            {
                return Enumerable.Empty<CodeFile>();
            }
        }

        public void Restore(Guid id)
        {
            var item = _context.CodeFiles.Find(id);
            if (item == null) return;
            item.IsDeleted = false;
            item.DeletedAt = null;
            try { lock (_saveLock) { _context.Update(item); _context.SaveChanges(); } } catch { }
        }

        private void PermanentlyRemoveFilesForId(Guid id)
        {
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
                                System.Diagnostics.Debug.WriteLine($"CodeRepository: permanently removed exported file '{p}' for id={id}");
                            }
                        }
                        catch { }
                    }
                }

                // Also attempt to delete originating files in user code directories
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
                                        try { File.Delete(file); System.Diagnostics.Debug.WriteLine($"CodeRepository: permanently removed originating file '{file}' for id={id}"); } catch { }
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

        public void AddTag(string name)
        {
            // Tag persistence moved to SettingsService (JSON-backed CodeFilters) for single source of truth.
            try
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                var trimmed = name.Trim();
                var filters = _settingsService?.GetCodeFilters() ?? new List<Pivot.Models.CustomFilter>();
                if (filters.Any(f => string.Equals(f.Name, trimmed, StringComparison.OrdinalIgnoreCase))) return;
                filters.Add(new Pivot.Models.CustomFilter { Name = trimmed });
                _settingsService?.SetCodeFiltersAsync(filters).ConfigureAwait(false);
            }
            catch { }
        }

        public string GetFilterNameById(Guid filterId)
        {
            return _settingsService.GetFilterNameById(filterId);
        }

        public void UpdateTagInAllSnippets(string oldTagName, string newTagName)
        {
            if (string.IsNullOrWhiteSpace(oldTagName) || string.IsNullOrWhiteSpace(newTagName)) return;
            if (string.Equals(oldTagName, newTagName, StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                var allSnippets = GetAll().ToList();
                var updated = false;

                foreach (var snippet in allSnippets)
                {
                    if (string.IsNullOrWhiteSpace(snippet.Tags)) continue;

                    var tags = snippet.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .ToList();

                    var hasOldTag = tags.Any(t => string.Equals(t, oldTagName, StringComparison.OrdinalIgnoreCase));
                    if (!hasOldTag) continue;

                    // Replace old tag with new tag
                    for (int i = 0; i < tags.Count; i++)
                    {
                        if (string.Equals(tags[i], oldTagName, StringComparison.OrdinalIgnoreCase))
                        {
                            tags[i] = newTagName.Trim();
                        }
                    }

                    snippet.Tags = string.Join(", ", tags);
                    snippet.Updated = DateTime.Now;
                    Save(snippet);
                    updated = true;
                }

                if (updated)
                {
                    System.Diagnostics.Debug.WriteLine($"UpdateTagInAllSnippets: Updated tag '{oldTagName}' to '{newTagName}' in snippets");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateTagInAllSnippets: Error: {ex.Message}");
            }
        }

        public void RemoveTagFromAllSnippets(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return;

            try
            {
                var allSnippets = GetAll().ToList();
                var updated = false;

                foreach (var snippet in allSnippets)
                {
                    if (string.IsNullOrWhiteSpace(snippet.Tags)) continue;

                    var tags = snippet.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .Where(t => !string.Equals(t, tagName, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (tags.Count == snippet.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Length)
                        continue; // Tag was not present

                    snippet.Tags = tags.Count > 0 ? string.Join(", ", tags) : string.Empty;
                    snippet.Updated = DateTime.Now;
                    Save(snippet);
                    updated = true;
                }

                if (updated)
                {
                    System.Diagnostics.Debug.WriteLine($"RemoveTagFromAllSnippets: Removed tag '{tagName}' from snippets");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoveTagFromAllSnippets: Error: {ex.Message}");
            }
        }
        
        public IEnumerable<Pivot.CodeModule.Models.CodeTag> GetAllTags()
        {
            try
            {
                var list = new List<Pivot.CodeModule.Models.CodeTag>();
                var filters = _settingsService?.GetCodeFilters() ?? new List<Pivot.Models.CustomFilter>();
                int idx = 1;
                foreach (var f in filters.OrderBy(ff => ff.SortOrder).ThenBy(ff => ff.Name))
                {
                    list.Add(new Pivot.CodeModule.Models.CodeTag { Id = idx++, Name = f.Name ?? string.Empty });
                }
                return list;
            }
            catch
            {
                return Enumerable.Empty<Pivot.CodeModule.Models.CodeTag>();
            }
        }
    }
}


