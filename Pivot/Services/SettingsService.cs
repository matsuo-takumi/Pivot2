using Microsoft.Extensions.Configuration;
using Pivot.Models;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using Microsoft.UI.Xaml; // ElementThemeを使用するために追加
using System.Collections.Generic; // Listを使用するために追加
using Microsoft.Extensions.Logging; // Loggerを使用するために追加
using Microsoft.UI.Xaml.Controls; // IMessengerを使用するために追加
using System.Linq; // ToListを使用するために追加
using CommunityToolkit.Mvvm.Messaging; // IMessengerを使用するために追加

namespace Pivot.Services
{
    public class SettingsService
    {
        // private readonly IConfiguration _configuration; // 使用しないため削除
        private readonly ILogger<SettingsService> _logger;
        private readonly MetadataService _metadataService;
        private readonly IMessenger _messenger;

        // In-memory cache to avoid sync-over-async and improve UI responsiveness
        private UserSettings _cache; // Settingsをメモリにキャッシュ

        public SettingsService(
            ILogger<SettingsService> logger,
            MetadataService metadataService,
            IMessenger messenger)
        {
            _logger = logger;
            _metadataService = metadataService;
            _messenger = messenger;
            _cache = new UserSettings(); // 初期キャッシュ
        }

        private async Task EnsureDbAsync()
        {
            if (_metadataService.Database == null)
            {
                await _metadataService.InitializeDatabase();
            }
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("SettingsService: Initializing...");
            await EnsureDbAsync();
            await LoadSettingsFromDbAsync();
            _logger.LogInformation("SettingsService: Initialization complete. AssetDirectories count: {AssetCount}", _cache.AssetDirectories.Count);
        }

        private async Task LoadSettingsFromDbAsync()
        {
            _logger.LogInformation("SettingsService: Loading settings from DB...");

            // AssetDirectories
            var assetPref = await _metadataService.GetPreferenceAsync("AssetDirectories");
            _cache.AssetDirectories = ParseDirectoriesValue(assetPref?.Value);
            _logger.LogInformation("SettingsService: Loaded AssetDirectories count from DB (parsed): {Count}", _cache.AssetDirectories.Count);
            await NormalizeAndPersistIfNeededAsync("AssetDirectories", assetPref?.Value, _cache.AssetDirectories);

            // ImageDirectories
            var imagePref = await _metadataService.GetPreferenceAsync("ImageDirectories");
            _cache.ImageDirectories = ParseDirectoriesValue(imagePref?.Value);
            _logger.LogInformation("SettingsService: Loaded ImageDirectories count from DB (parsed): {Count}", _cache.ImageDirectories.Count);
            await NormalizeAndPersistIfNeededAsync("ImageDirectories", imagePref?.Value, _cache.ImageDirectories);

            // ProjectDirectories
            var projectPref = await _metadataService.GetPreferenceAsync("ProjectDirectories");
            _cache.ProjectDirectories = ParseDirectoriesValue(projectPref?.Value);
            _logger.LogInformation("SettingsService: Loaded ProjectDirectories count from DB (parsed): {Count}", _cache.ProjectDirectories.Count);
            await NormalizeAndPersistIfNeededAsync("ProjectDirectories", projectPref?.Value, _cache.ProjectDirectories);

            if (Enum.TryParse<ElementTheme>((await _metadataService.GetPreferenceAsync("AppTheme"))?.Value, out var theme))
            {
                _cache.AppTheme = theme;
            }
            else
            {
                _cache.AppTheme = ElementTheme.Default;
            }
            _logger.LogInformation("SettingsService: Loaded AppTheme: {Theme}", _cache.AppTheme);

            if (Enum.TryParse<BackdropType>((await _metadataService.GetPreferenceAsync("AppBackdropType"))?.Value, out var backdrop))
            {
                _cache.AppBackdropType = backdrop;
            }
            else
            {
                _cache.AppBackdropType = BackdropType.Mica;
            }
            _logger.LogInformation("SettingsService: Loaded AppBackdropType: {BackdropType}", _cache.AppBackdropType);

            // Asset 表示モード
            try
            {
                var modeStr = (await _metadataService.GetPreferenceAsync("AssetDisplayMode"))?.Value;
                if (Enum.TryParse<AssetDisplayMode>(modeStr, out var mode))
                {
                    _cache.AssetDisplayMode = mode;
                }
                else
                {
                    _cache.AssetDisplayMode = AssetDisplayMode.List;
                }
                _logger.LogInformation("SettingsService: Loaded AssetDisplayMode: {Mode}", _cache.AssetDisplayMode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load AssetDisplayMode. Using default.");
                _cache.AssetDisplayMode = AssetDisplayMode.List;
            }

            // Asset メタデータ表示
            try
            {
                var showMetaStr = (await _metadataService.GetPreferenceAsync("ShowAssetMetadata"))?.Value;
                if (bool.TryParse(showMetaStr, out var show))
                {
                    _cache.ShowAssetMetadata = show;
                }
                else
                {
                    _cache.ShowAssetMetadata = true;
                }
                _logger.LogInformation("SettingsService: Loaded ShowAssetMetadata: {Show}", _cache.ShowAssetMetadata);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load ShowAssetMetadata. Using default.");
                _cache.ShowAssetMetadata = true;
            }

            // スキャン戦略: ForceFullScan
            try
            {
                var forceFullStr = (await _metadataService.GetPreferenceAsync("Scan.ForceFull"))?.Value;
                if (bool.TryParse(forceFullStr, out var force))
                {
                    _cache.ForceFullScan = force;
                }
                else
                {
                    _cache.ForceFullScan = false;
                }
                _logger.LogInformation("SettingsService: Loaded ForceFullScan: {Force}", _cache.ForceFullScan);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load ForceFullScan. Using default (false).");
                _cache.ForceFullScan = false;
            }

            // メニュー表示モード
            try
            {
                var modeStr = (await _metadataService.GetPreferenceAsync("MenuDisplayMode"))?.Value;
                if (Enum.TryParse<MenuDisplayMode>(modeStr, out var mode))
                {
                    _cache.MenuDisplayMode = mode;
                }
                else
                {
                    _cache.MenuDisplayMode = MenuDisplayMode.Compact;
                }
                _logger.LogInformation("SettingsService: Loaded MenuDisplayMode: {Mode}", _cache.MenuDisplayMode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load MenuDisplayMode. Using default (Compact).");
                _cache.MenuDisplayMode = MenuDisplayMode.Compact;
            }
        }

        private static bool LooksLikeJsonArray(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var v = value.TrimStart();
            return v.Length > 0 && v[0] == '[';
        }

        private List<string> ParseDirectoriesValue(string? storedValue)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(storedValue)) return result;

            try
            {
                var value = storedValue.Trim();
                if (LooksLikeJsonArray(value))
                {
                    AddFromJson(value, result, 0);
                }
                else
                {
                    // Legacy '|' delimited string
                    result = value
                        .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to parse directories value. Falling back to '|' split.");
                result = (storedValue ?? string.Empty)
                    .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            // Final cleanup: drop empty and obvious garbage tokens
            result = result
                .Where(s => !string.IsNullOrWhiteSpace(s) && s != "[]")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return result;
        }

        private void AddFromJson(string json, List<string> dest, int depth)
        {
            if (depth > 6) return; // avoid endless unwrap
            try
            {
                var arr = JsonSerializer.Deserialize<List<string>>(json);
                if (arr == null) return;

                foreach (var item in arr)
                {
                    if (string.IsNullOrWhiteSpace(item)) continue;
                    var t = item.Trim();
                    if (LooksLikeJsonArray(t))
                    {
                        AddFromJson(t, dest, depth + 1);
                    }
                    else
                    {
                        dest.Add(t);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: JSON parse error at depth {Depth}", depth);
            }
        }

        private async Task NormalizeAndPersistIfNeededAsync(string key, string? originalStoredValue, List<string> directories)
            {
                try
                {
                var normalized = JsonSerializer.Serialize(directories);
                if (!string.Equals((originalStoredValue ?? string.Empty).Trim(), normalized, StringComparison.Ordinal))
                {
                    _logger.LogInformation("SettingsService: Normalizing and persisting {Key}. Old='{Old}', New='{New}'", key, originalStoredValue, normalized);
                    await _metadataService.UpsertPreferenceAsync(key, normalized);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to normalize/persist {Key}", key);
            }
        }

        // Synchronous getters now use in-memory cache
        public UserSettings GetUserSettings()
        {
            _logger.LogDebug("SettingsService: GetUserSettings called. Current AssetDirectories count: {Count}", _cache.AssetDirectories.Count);
            return _cache;
        }

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

        // Asset 表示モード/メタ表示 設定
        public AssetDisplayMode GetAssetDisplayMode() => _cache.AssetDisplayMode;

        public async Task SetAssetDisplayModeAsync(AssetDisplayMode mode)
        {
            await EnsureDbAsync();
            _cache.AssetDisplayMode = mode;
            await _metadataService.UpsertPreferenceAsync("AssetDisplayMode", mode.ToString());
        }

        public bool GetShowAssetMetadata() => _cache.ShowAssetMetadata;

        public async Task SetShowAssetMetadataAsync(bool show)
        {
            await EnsureDbAsync();
            _cache.ShowAssetMetadata = show;
            await _metadataService.UpsertPreferenceAsync("ShowAssetMetadata", show.ToString());
        }

        // スキャン戦略設定
        public bool GetForceFullScan() => _cache.ForceFullScan;

        public async Task SetForceFullScanAsync(bool force)
        {
            await EnsureDbAsync();
            _cache.ForceFullScan = force;
            await _metadataService.UpsertPreferenceAsync("Scan.ForceFull", force.ToString());
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

        public MenuDisplayMode GetMenuDisplayMode() => _cache.MenuDisplayMode;

        public async Task SetMenuDisplayModeAsync(MenuDisplayMode mode)
        {
            await EnsureDbAsync();
            _cache.MenuDisplayMode = mode;
            await _metadataService.UpsertPreferenceAsync("MenuDisplayMode", mode.ToString());
        }
    }
}
