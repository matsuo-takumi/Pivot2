using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pivot.Engine.Models;
using Pivot.Repositories;
using Pivot.Services;

namespace Pivot.Services
{
    /// <summary>
    /// Data Access Layer for Code Snippets using AssetEntity as the single source of truth.
    /// </summary>
    public class CodeService
    {
        private readonly IAssetRepository _repository;
        private readonly ILogger<CodeService> _logger;
        private readonly DirectorySettingsService _directorySettings;
        private readonly CodeTagService _tagService;

        public CodeService(
            IAssetRepository repository,
            ILogger<CodeService> logger,
            DirectorySettingsService directorySettings,
            CodeTagService tagService)
        {
            _repository = repository;
            _logger = logger;
            _directorySettings = directorySettings;
            _tagService = tagService;
        }

        public async Task<List<AssetEntity>> GetAllSnippetsAsync()
        {
            try
            {
                // Retrieve all code-related assets using Repository
                // Kind = Script (alias Code)
                // Fetch reasonably large number (e.g. 50000) as "All"
                var assets = await _repository.GetPagedAsync(kind: AssetKind.Code, take: 50000);
                
                // Further filter client-side if needed (e.g. excluding deleted if repository returns them?)
                // Repository implementation of GetPagedAsync typically filters IsDeleted=0?
                // Assuming GetPagedAsync returns active assets.
                
                return assets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load snippets.");
                return new List<AssetEntity>();
            }
        }

        public async Task<AssetEntity?> SaveSnippetAsync(AssetEntity snippet, bool saveToDisk = true)
        {
            if (snippet == null) return null;

            var updatedSnippet = snippet with
            {
                LastModifiedUtc = DateTime.UtcNow,
                Kind = AssetKind.Code
            };

            try
            {
                // 1. Save to File System (Source of Truth)
                if (saveToDisk && !string.IsNullOrWhiteSpace(snippet.FilePath))
                {
                    var dir = Path.GetDirectoryName(snippet.FilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    // Writing content is handled by ViewModel or checking snippet.Content property if I add it to extension
                }

                // 2. Upsert to DB
                await _repository.UpsertAsync(snippet);

                return snippet;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save snippet.");
                return null;
            }
        }

        public async Task DeleteSnippetAsync(AssetEntity snippet)
        {
            if (snippet == null || string.IsNullOrEmpty(snippet.FilePath)) return;
            
            try
            {
                // 1. Delete file from filesystem
                if (System.IO.File.Exists(snippet.FilePath))
                {
                    System.IO.File.Delete(snippet.FilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete snippet file: {Path}", snippet.FilePath);
                // Continue with DB deletion even if file delete fails
            }
            
            // 2. Hard delete from database
            await _repository.HardDeleteByPathAsync(snippet.FilePath);
        }

        public async Task RestoreSnippetAsync(AssetEntity snippet)
        {
            if (snippet == null) return;

            snippet.IsDeleted = false;
            snippet.DeletedAt = null;

            await _repository.UpsertAsync(snippet);
        }
        
        // HardDelete removed as it requires direct DB access not exposed by IAssetRepository
        // Logic should rely on Soft Delete + external cleanup or implementing a proper Delete method in Repository later.

        public async Task RemoveTagGloballyAsync(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return;

            var normalizedTag = _tagService.NormalizeTagName(tagName);
            if (string.IsNullOrEmpty(normalizedTag)) return;

            // Fetch all snippets (using a large page size to cover most use cases)
            var snippets = await GetAllSnippetsAsync();

            int changedCount = 0;
            foreach (var snippet in snippets)
            {
                if (_tagService.RemoveTagFromSnippet(snippet, normalizedTag))
                {
                    // If tag was removed, save the changes (DB + File logic via SaveSnippetAsync)
                    await SaveSnippetAsync(snippet, saveToDisk: true);
                    changedCount++;
                }
            }
            
            _logger.LogInformation("Removed tag '{TagName}' from {Count} snippets.", normalizedTag, changedCount);
        }

        /// <summary>
        /// Reads the content of a code snippet from disk.
        /// </summary>
        public async Task<string> ReadContentAsync(AssetEntity snippet)
        {
            if (snippet == null || string.IsNullOrEmpty(snippet.FilePath))
                return string.Empty;

            try
            {
                if (File.Exists(snippet.FilePath))
                {
                    return await File.ReadAllTextAsync(snippet.FilePath);
                }
                else if (!string.IsNullOrEmpty(snippet.ContentIndex))
                {
                    return snippet.ContentIndex;
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read content from {Path}", snippet.FilePath);
                return string.Empty;
            }
        }

        /// <summary>
        /// Writes content to a code snippet file on disk.
        /// </summary>
        public async Task WriteContentAsync(AssetEntity snippet, string content)
        {
            if (snippet == null || string.IsNullOrEmpty(snippet.FilePath))
                return;

            try
            {
                var dir = Path.GetDirectoryName(snippet.FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                await File.WriteAllTextAsync(snippet.FilePath, content);
                
                // Update metadata
                // Update metadata (create new record for DB update)
                var updatedSnippet = snippet with 
                {
                    ContentIndex = content.Length > 500 ? content.Substring(0, 500) : content,
                    FileSize = new FileInfo(snippet.FilePath).Length,
                    UpdatedAt = DateTime.UtcNow
                };

                // IMPORTANT: We must update the DB with the new record
                // (The caller's 'snippet' variable remains unchanged due to immutability)
                // Attempting to recursively call SaveSnippetAsync or UpsertAsync
                await _repository.UpsertAsync(updatedSnippet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write content to {Path}", snippet.FilePath);
                throw;
            }
        }
    }
}
