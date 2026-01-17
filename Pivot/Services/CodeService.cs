using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pivot.Models;
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

        public CodeService(
            IAssetRepository repository,
            ILogger<CodeService> logger,
            DirectorySettingsService directorySettings)
        {
            _repository = repository;
            _logger = logger;
            _directorySettings = directorySettings;
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

            snippet.LastModifiedUtc = DateTime.UtcNow;
            snippet.Kind = AssetKind.Code; // Ensure kind is set

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
    }
}
