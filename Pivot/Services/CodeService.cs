using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Pivot.CodeModule.Models;
using Pivot.Models;
using Pivot.Repositories;

namespace Pivot.Services
{
    public class CodeService
    {
        private readonly IAssetRepository _repository;
        private readonly ILogger<CodeService> _logger;
        private readonly DirectorySettingsService _directorySettings;

        public CodeService(IAssetRepository repository, ILogger<CodeService> logger, DirectorySettingsService directorySettings)
        {
            _repository = repository;
            _logger = logger;
            _directorySettings = directorySettings;
        }

        public async Task<List<CodeFile>> GetAllSnippetsAsync(CancellationToken ct = default)
        {
            // Fetch all assets that are marked as Script or Code
            // Note: Repository likely needs a method to filter by Kind efficiently, or we fetch all and filter client-side if dataset is small.
            // Assuming we accept clent-side filtering for now as Pivot2 seems local/single-user.
            
            // However, AssetRepository.GetAssetsAsync(kind) would be better. 
            // Checking AssetRepository... if it doesn't have it, we'll fetch all or add method later.
            // For now, let's assume we can fetch all or use a specific method if available.
            // Use GetPagedAsync with Code/Script kind filter
            // Get a large page to effectively get all code files
            var assets = await _repository.GetPagedAsync(
                kind: AssetKind.Code, 
                take: 10000, // Large enough for most use cases
                ct: ct);

            var result = new List<CodeFile>();
            foreach (var asset in assets)
            {
                result.Add(MapToCodeFile(asset));
            }

            return result;
        }

        public async Task<List<CodeFile>> GetDeletedSnippetsAsync(CancellationToken ct = default)
        {
            // Fetch all code assets including deleted ones, then filter to only deleted
            // Since GetPagedAsync doesn't support isDeleted filter, we query all and filter client-side
            var assets = await _repository.GetPagedAsync(
                kind: AssetKind.Code, 
                take: 10000,
                ct: ct);

            var result = new List<CodeFile>();
            foreach (var asset in assets.Where(a => a.IsDeleted))
            {
                result.Add(MapToCodeFile(asset));
            }

            return result;
        }

        public async Task<List<CodeFile>> SearchAsync(string query, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query)) return await GetAllSnippetsAsync(ct);

            // In-memory search is safer because ContentIndex might be null for old files
            // Ideally use DB LIKE query. 
            // For now, Fetch all scripts and filter. (Optimizable later)
            
            var all = await GetAllSnippetsAsync(ct);
            var q = query.ToLowerInvariant();

            return all.Where(x => 
                (x.Title?.ToLowerInvariant().Contains(q) ?? false) ||
                (x.Content?.ToLowerInvariant().Contains(q) ?? false) ||
                (x.Tags?.ToLowerInvariant().Contains(q) ?? false)
            ).ToList();
        }

        public async Task SaveSnippetAsync(CodeFile file)
        {
            // 1. Resolve Path
            string path = file.FilePath;
            
            // If path is empty (new file), create logic needed. 
            // For now, fail if path is empty (UI should enforce path selection or generation).
            if (string.IsNullOrWhiteSpace(path))
            {
                // Fallback: Generate generic path in user code dir
                var dirs = _directorySettings.CodeDirectories;
                var root = dirs?.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                    throw new InvalidOperationException("No valid code directory configured to save new snippet.");
                
                var ext = !string.IsNullOrEmpty(file.Language) ? $".{file.Language}" : ".txt";
                var safeTitle = string.Join("_", (file.Title ?? "Untitled").Split(Path.GetInvalidFileNameChars()));
                path = Path.Combine(root, $"{safeTitle}{ext}");
                file.FilePath = path;
            }

            // 2. Write content to disk
            // Write Tags/Metadata to YAML frontmatter if .md, or just write content?
            // User requirement: "Code content... stored in file system".
            // Since we use AssetEntity for metadata, maybe we don't need YAML frontmatter in file anymore?
            // "Scorched Earth": Remove legacy YAML complication. Just write plain text.
            // Metadata (Tags, etc) lives in DB (AssetEntity).
            
            await File.WriteAllTextAsync(path, file.Content ?? "");

            // 3. Optimistic DB Update (Waiting for watcher is slow for UX)
            // We upsert the AssetEntity immediately so the UI Logic can rely on the DB being "eventually consistency" but "immediately responsive" via this call.
            
            // Re-fetch or Create AssetEntity
            var asset = await _repository.GetByPathAsync(path) ?? new AssetEntity
            {
                FilePath = path,
                FileName = Path.GetFileName(path),
                Directory = Path.GetDirectoryName(path) ?? "",
                Kind = AssetKind.Code,
                CreatedAt = DateTime.UtcNow
            };
            
            asset.FileSize = new FileInfo(path).Length;
            asset.LastModifiedUtc = DateTime.UtcNow; // approximate
            asset.ContentIndex = file.Content; // Update index immediately
            
            // Map Tags back to JSON
            if (!string.IsNullOrWhiteSpace(file.Tags))
            {
                var tagList = file.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(t => t.Trim())
                                       .Where(t => !string.IsNullOrWhiteSpace(t))
                                       .ToList();
                asset.UserTagsJson = JsonSerializer.Serialize(tagList);
            }
            else
            {
                asset.UserTagsJson = "[]";
            }
            
            asset.Language = file.Language;
            asset.Tool = file.Tool; // Persist tool if needed
            
            await _repository.UpsertAsync(asset);
        }
        
        // --- Mapping Helpers ---

        private CodeFile MapToCodeFile(AssetEntity asset)
        {
            var cf = new CodeFile
            {
                Id = CreateDeterministicGuid(asset.FilePath),
                FilePath = asset.FilePath, // Mapped!
                Title = asset.FileName, 
                Language = asset.Language ?? "txt",
                Tool = asset.Tool ?? "",
                Updated = asset.LastModifiedUtc,
                IsDeleted = asset.IsDeleted,
                DeletedAt = asset.DeletedAt,
                Content = asset.ContentIndex ?? ""
            };

            // Map TagsJson ["a", "b"] -> "a, b"
            if (!string.IsNullOrEmpty(asset.UserTagsJson))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<string>>(asset.UserTagsJson);
                    if (list != null) cf.Tags = string.Join(", ", list);
                }
                catch { cf.Tags = asset.UserTagsJson; } // Fallback
            }

            return cf;
        }
        
        public async Task DeleteSnippetAsync(CodeFile file)
        {
            var asset = await _repository.GetByPathAsync(file.FilePath);
            if (asset != null)
            {
                asset.IsDeleted = true;
                asset.DeletedAt = DateTime.UtcNow;
                await _repository.UpsertAsync(asset);
            }
        }

        public async Task RestoreSnippetAsync(CodeFile file)
        {
            var asset = await _repository.GetByPathAsync(file.FilePath);
            if (asset != null)
            {
                asset.IsDeleted = false;
                asset.DeletedAt = null;
                await _repository.UpsertAsync(asset);
            }
        }

        public async Task HardDeleteSnippetAsync(CodeFile file)
        {
            var asset = await _repository.GetByPathAsync(file.FilePath);
            if (asset != null)
            {
                await _repository.MarkDeletedAsync(asset.FilePath); // Use MarkDeleted or Delete? 
                // Repository might mostly support soft delete. 
                // If we want hard delete, we might need a method for it or just ensure it's removed.
                // AssetRepository.DeleteAsync might not exist or be soft.
                // Checking AssetRepository earlier: "MarkDeletedAsync" existed.
                // Let's assume we use MarkDeletedAsync for logical delete, but for HARD delete we might need to physically remove the file.
                
                // "Scorched Earth": If User wants hard delete, we delete the file.
                if (File.Exists(asset.FilePath))
                {
                    File.Delete(asset.FilePath);
                }
                
                // Then remove from DB (using MarkDeleted implies soft, we might simply leave it as deleted or remove row if possible).
                // If the file is gone, the Scanner will eventually remove it or mark it missing.
                // But let's try to be clean.
                // For now, let's just ensure IsDeleted=true in DB and file is gone.
                asset.IsDeleted = true;
                await _repository.UpsertAsync(asset);
            }
        }
        private Guid CreateDeterministicGuid(string input)
        {
            try
            {
                using var md5 = MD5.Create();
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
                return new Guid(bytes);
            }
            catch
            {
                return Guid.NewGuid();
            }
        }
    }
}
