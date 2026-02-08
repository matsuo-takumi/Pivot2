using Pivot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Pivot.Messages;
using Pivot.Engine.Models;

namespace Pivot.Services
{
    public class DirectorySettingsService
    {
        private readonly ILogger<DirectorySettingsService> _logger;
        private readonly ISettingsStore _settingsStore;
        private readonly IMessenger _messenger;

        // Internal cache for directories
        private List<string> _assetDirectories = new();
        private List<string> _imageDirectories = new();
        private List<string> _projectDirectories = new();
        private List<string> _codeDirectories = new();

        public IReadOnlyList<string> AssetDirectories => _assetDirectories;
        public IReadOnlyList<string> ImageDirectories => _imageDirectories;
        public IReadOnlyList<string> ProjectDirectories => _projectDirectories;
        public IReadOnlyList<string> CodeDirectories => _codeDirectories;

        // Migrated Code Save Output Directory
        public string CodeSaveOutputDirectory { get; private set; } = string.Empty;

        public DirectorySettingsService(
            ILogger<DirectorySettingsService> logger,
            ISettingsStore settingsStore,
            IMessenger messenger)
        {
            _logger = logger;
            _settingsStore = settingsStore;
            _messenger = messenger;
        }

        public async Task LoadAsync()
        {
            _logger.LogInformation("DirectorySettingsService: Loading directory settings...");

            // AssetDirectories
            var assetPref = await _settingsStore.GetAsync("AssetDirectories");
            _assetDirectories = ParseDirectoriesValue(assetPref);
            await NormalizeAndPersistIfNeededAsync("AssetDirectories", assetPref, _assetDirectories);

            // ImageDirectories
            var imagePref = await _settingsStore.GetAsync("ImageDirectories");
            _imageDirectories = ParseDirectoriesValue(imagePref);
            await NormalizeAndPersistIfNeededAsync("ImageDirectories", imagePref, _imageDirectories);

            // ProjectDirectories
            var projectPref = await _settingsStore.GetAsync("ProjectDirectories");
            _projectDirectories = ParseDirectoriesValue(projectPref);
            await NormalizeAndPersistIfNeededAsync("ProjectDirectories", projectPref, _projectDirectories);

            // CodeDirectories
            var codePref = await _settingsStore.GetAsync("CodeDirectories");
            _codeDirectories = ParseDirectoriesValue(codePref);
            await NormalizeAndPersistIfNeededAsync("CodeDirectories", codePref, _codeDirectories);

            // CodeSaveOutputDirectory
            await LoadCodeSaveOutputDirectoryAsync();
        }

        private async Task LoadCodeSaveOutputDirectoryAsync()
        {
            try
            {
                var codeSaveDir = await _settingsStore.GetAsync("Code.Save.OutputDirectory");
                if (string.IsNullOrWhiteSpace(codeSaveDir))
                {
                    // Fallback migration
                    codeSaveDir = await _settingsStore.GetAsync("Export.OutputDirectory");
                    if (!string.IsNullOrWhiteSpace(codeSaveDir))
                    {
                        try { await _settingsStore.UpsertAsync("Code.Save.OutputDirectory", codeSaveDir); } catch { }
                    }
                }
                CodeSaveOutputDirectory = codeSaveDir ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load Code.Save.OutputDirectory");
                CodeSaveOutputDirectory = string.Empty;
            }
        }

        public async Task SetCodeSaveOutputDirectoryAsync(string path)
        {
            CodeSaveOutputDirectory = path ?? string.Empty;
            await _settingsStore.UpsertAsync("Code.Save.OutputDirectory", CodeSaveOutputDirectory);
        }

        public async Task AddDirectoryAsync(DirectoryCategory category, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            string key = GetKeyForCategory(category);
            var list = GetListForCategory(category);

            if (!list.Contains(path))
            {
                list.Add(path);
                await PersistListAsync(key, list);
                
                _messenger.Send(new DirectoryChangedMessage(new DirectoryChangedMessageData
                {
                    Category = category,
                    Path = path,
                    Type = DirectoryChangedMessageData.ChangeType.Added
                }));
            }
        }

        public async Task RemoveDirectoryAsync(DirectoryCategory category, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            string key = GetKeyForCategory(category);
            var list = GetListForCategory(category);

            if (list.Remove(path))
            {
                await PersistListAsync(key, list);
                
                _messenger.Send(new DirectoryChangedMessage(new DirectoryChangedMessageData
                {
                    Category = category,
                    Path = path,
                    Type = DirectoryChangedMessageData.ChangeType.Removed
                }));
            }
        }

        private async Task PersistListAsync(string key, List<string> list)
        {
            try
            {
                string serializedList = JsonSerializer.Serialize(list);
                await _settingsStore.UpsertAsync(key, serializedList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist directory list for {Key}", key);
            }
        }

        private string GetKeyForCategory(DirectoryCategory category) => category switch
        {
            DirectoryCategory.Asset => "AssetDirectories",
            DirectoryCategory.Image => "ImageDirectories",
            DirectoryCategory.Project => "ProjectDirectories",
            DirectoryCategory.Code => "CodeDirectories",
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };

        private List<string> GetListForCategory(DirectoryCategory category) => category switch
        {
            DirectoryCategory.Asset => _assetDirectories,
            DirectoryCategory.Image => _imageDirectories,
            DirectoryCategory.Project => _projectDirectories,
            DirectoryCategory.Code => _codeDirectories,
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };

        private List<string> ParseDirectoriesValue(string? storedValue)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(storedValue)) return result;

            try
            {
                var value = storedValue.Trim();
                if (LooksLikeJsonArray(value))
                {
                    var parsed = JsonSerializer.Deserialize<List<string>>(value);
                    if (parsed != null) result.AddRange(parsed);
                }
                else
                {
                    // Legacy '|' delimited
                    result = value.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }
            catch
            {
                // Fallback
                 result = (storedValue ?? string.Empty)
                    .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return result
                .Where(s => !string.IsNullOrWhiteSpace(s) && s != "[]")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private bool LooksLikeJsonArray(string value)
        {
            return value.StartsWith("[") && value.EndsWith("]");
        }

        private async Task NormalizeAndPersistIfNeededAsync(string key, string? originalStoredValue, List<string> directories)
        {
            try
            {
                var normalized = JsonSerializer.Serialize(directories);
                if (!string.Equals((originalStoredValue ?? string.Empty).Trim(), normalized, StringComparison.Ordinal))
                {
                    await _settingsStore.UpsertAsync(key, normalized);
                }
            }
            catch { }
        }
        public Task<List<string>> GetAllDirectoriesAsync()
        {
            var allDirs = new List<string>();
            allDirs.AddRange(_assetDirectories);
            allDirs.AddRange(_imageDirectories);
            allDirs.AddRange(_projectDirectories);
            allDirs.AddRange(_codeDirectories);
            return Task.FromResult(allDirs.Distinct().ToList());
        }
    }
}
