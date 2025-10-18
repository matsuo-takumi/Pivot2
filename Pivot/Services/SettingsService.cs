using Microsoft.Extensions.Configuration;
using Pivot.Models;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using Microsoft.UI.Xaml; // ElementThemeを使用するために追加
using System.Collections.Generic; // Listを使用するために追加
using Microsoft.Extensions.Logging; // Loggerを使用するために追加

namespace Pivot.Services
{
    public class SettingsService
    {
        private readonly IConfiguration _configuration;
        private readonly MetadataService _metadataService;
        private readonly ILogger<SettingsService> _logger; // Loggerを追加

        // In-memory cache to avoid sync-over-async and improve UI responsiveness
        private readonly UserSettings _cache = new UserSettings();

        public SettingsService(IConfiguration configuration, MetadataService metadataService, ILogger<SettingsService> logger) // Loggerを追加
        {
            _configuration = configuration;
            _metadataService = metadataService;
            _logger = logger; // Loggerを初期化
            // Note: Do NOT block here. Call InitializeAsync from App startup sequence.
        }

        private async Task EnsureDbAsync()
        {
            if (_metadataService.Database == null)
            {
                await _metadataService.InitializeDatabase();
            }
        }

        // One-shot async initialization to load defaults and hydrate cache
        public async Task InitializeAsync()
        {
            await EnsureDbAsync();

            _logger.LogDebug("[SettingsService] Initializing default settings...");

            // Ensure defaults
            if (await _metadataService.GetPreferenceAsync("AppTheme") == null)
            {
                await SetTheme(ElementTheme.Default);
                _logger.LogDebug("[SettingsService] Set default AppTheme to: {Theme}", ElementTheme.Default);
            }
            if (await _metadataService.GetPreferenceAsync("AppBackdropType") == null)
            {
                await SetBackdropType(BackdropType.Mica);
                _logger.LogDebug("[SettingsService] Set default AppBackdropType to: {Backdrop}", BackdropType.Mica);
            }
            if (await _metadataService.GetPreferenceAsync("AssetDirectories") == null)
            {
                await _metadataService.UpsertPreferenceAsync("AssetDirectories", JsonSerializer.Serialize(new List<string>()));
                _logger.LogDebug("[SettingsService] Set default AssetDirectories.");
            }
            if (await _metadataService.GetPreferenceAsync("ImageDirectories") == null)
            {
                await _metadataService.UpsertPreferenceAsync("ImageDirectories", JsonSerializer.Serialize(new List<string>()));
                _logger.LogDebug("[SettingsService] Set default ImageDirectories.");
            }
            if (await _metadataService.GetPreferenceAsync("ProjectDirectories") == null)
            {
                await _metadataService.UpsertPreferenceAsync("ProjectDirectories", JsonSerializer.Serialize(new List<string>()));
                _logger.LogDebug("[SettingsService] Set default ProjectDirectories.");
            }

            // Hydrate cache from DB
            _logger.LogDebug("[SettingsService] Hydrating cache from DB...");
            var themeEntry = await _metadataService.GetPreferenceAsync("AppTheme");
            if (themeEntry != null)
            {
                _cache.AppTheme = Enum.Parse<ElementTheme>(themeEntry.Value);
            }
            var backdropEntry = await _metadataService.GetPreferenceAsync("AppBackdropType");
            if (backdropEntry != null)
            {
                _cache.AppBackdropType = Enum.Parse<BackdropType>(backdropEntry.Value);
            }
            var assetDirsEntry = await _metadataService.GetPreferenceAsync("AssetDirectories");
            if (assetDirsEntry != null)
            {
                _cache.AssetDirectories = JsonSerializer.Deserialize<List<string>>(assetDirsEntry.Value) ?? new List<string>();
            }
            var imageDirsEntry = await _metadataService.GetPreferenceAsync("ImageDirectories");
            if (imageDirsEntry != null)
            {
                _cache.ImageDirectories = JsonSerializer.Deserialize<List<string>>(imageDirsEntry.Value) ?? new List<string>();
            }
            var projectDirsEntry = await _metadataService.GetPreferenceAsync("ProjectDirectories");
            if (projectDirsEntry != null)
            {
                _cache.ProjectDirectories = JsonSerializer.Deserialize<List<string>>(projectDirsEntry.Value) ?? new List<string>();
            }
        }

        // Synchronous getters now use in-memory cache
        public UserSettings GetUserSettings() => _cache;

        public ElementTheme GetTheme() => _cache.AppTheme;

        public async Task SetTheme(ElementTheme theme)
        {
            await EnsureDbAsync();
            _cache.AppTheme = theme;
            await _metadataService.UpsertPreferenceAsync("AppTheme", theme.ToString());
        }

        public BackdropType GetBackdropType() => _cache.AppBackdropType;

        public async Task SetBackdropType(BackdropType type)
        {
            await EnsureDbAsync();
            _cache.AppBackdropType = type;
            await _metadataService.UpsertPreferenceAsync("AppBackdropType", type.ToString());
        }

        public async Task AddDirectoryAsync(DirectoryCategory category, string path)
        {
            await EnsureDbAsync();
            if (string.IsNullOrWhiteSpace(path)) return;

            string key = category switch
            {
                DirectoryCategory.Asset => "AssetDirectories",
                DirectoryCategory.Image => "ImageDirectories",
                DirectoryCategory.Project => "ProjectDirectories",
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };

            var list = category switch
            {
                DirectoryCategory.Asset => _cache.AssetDirectories,
                DirectoryCategory.Image => _cache.ImageDirectories,
                DirectoryCategory.Project => _cache.ProjectDirectories,
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };

            _logger.LogDebug("[SettingsService] AddDirectoryAsync: Category={Category}, Path='{Path}', Key='{Key}'", category, path, key);
            _logger.LogDebug("[SettingsService] AddDirectoryAsync: Current cache list for {Key}: {List}", key, string.Join(", ", list));

            if (!list.Contains(path))
            {
                list.Add(path);
                string serializedList = JsonSerializer.Serialize(list);
                _logger.LogDebug("[SettingsService] AddDirectoryAsync: New serialized list for {Key}: {SerializedList}", key, serializedList);
                await _metadataService.UpsertPreferenceAsync(key, serializedList);
                _logger.LogDebug("[SettingsService] AddDirectoryAsync: Successfully upserted for {Key}.", key);
            }
            else
            {
                _logger.LogDebug("[SettingsService] AddDirectoryAsync: Path '{Path}' already exists in cache for {Key}.", path, key);
            }
        }

        public async Task RemoveDirectoryAsync(DirectoryCategory category, string path)
        {
            await EnsureDbAsync();
            if (string.IsNullOrWhiteSpace(path)) return;

            string key = category switch
            {
                DirectoryCategory.Asset => "AssetDirectories",
                DirectoryCategory.Image => "ImageDirectories",
                DirectoryCategory.Project => "ProjectDirectories",
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };

            var list = category switch
            {
                DirectoryCategory.Asset => _cache.AssetDirectories,
                DirectoryCategory.Image => _cache.ImageDirectories,
                DirectoryCategory.Project => _cache.ProjectDirectories,
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };

            _logger.LogDebug("[SettingsService] RemoveDirectoryAsync: Category={Category}, Path='{Path}', Key='{Key}'", category, path, key);
            _logger.LogDebug("[SettingsService] RemoveDirectoryAsync: Current cache list for {Key}: {List}", key, string.Join(", ", list));

            if (list.Remove(path))
            {
                string serializedList = JsonSerializer.Serialize(list);
                _logger.LogDebug("[SettingsService] RemoveDirectoryAsync: New serialized list for {Key}: {SerializedList}", key, serializedList);
                await _metadataService.UpsertPreferenceAsync(key, serializedList);
                _logger.LogDebug("[SettingsService] RemoveDirectoryAsync: Successfully upserted after removal for {Key}.", key);
            }
            else
            {
                _logger.LogDebug("[SettingsService] RemoveDirectoryAsync: Path '{Path}' not found in cache for {Key}.", path, key);
            }
        }
    }
}
