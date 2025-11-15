using Microsoft.Extensions.Configuration;
using Pivot.Models;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using Microsoft.UI.Xaml; // ElementThemeを使用するために追加
using System.Collections.Generic; // Listを使用するために追加
using Microsoft.Extensions.Logging; // Loggerを使用するために追加
using System.Globalization; // For invariant culture parsing/formatting
using System.Diagnostics; // Debug logging
using System.Linq; // ToListを使用するために追加
using CommunityToolkit.Mvvm.Messaging; // IMessengerを使用するために追加
using Pivot.Messages; // DirectoryChangedMessageを使用するために追加

namespace Pivot.Services
{
    public class SettingsService
    {
        // private readonly IConfiguration _configuration; // 使用しないため削除
        private readonly ILogger<SettingsService> _logger;
        private readonly MetadataService _metadataService;
        private readonly IMessenger _messenger;
        private readonly ISettingsStore _settingsStore;

        // In-memory cache to avoid sync-over-async and improve UI responsiveness
        private UserSettings _cache; // Settingsをメモリにキャッシュ

        public SettingsService(
            ILogger<SettingsService> logger,
            MetadataService metadataService,
            IMessenger messenger, // コンストラクタに IMessenger を追加
            ISettingsStore? settingsStore = null)
        {
            _logger = logger;
            _metadataService = metadataService;
            _messenger = messenger; // 初期化
            _settingsStore = settingsStore ?? new JsonSettingsStore();
            _cache = new UserSettings(); // 初期キャッシュ
        }

        // DB依存を排除し、JSON設定ストアに移行

        public async Task InitializeAsync()
        {
            _logger.LogInformation("SettingsService: Initializing...");
            await _settingsStore.InitializeAsync();
            await LoadSettingsFromStoreAsync();
            _logger.LogInformation("SettingsService: Initialization complete. AssetDirectories count: {AssetCount}", _cache.AssetDirectories.Count);
        }

        private async Task LoadSettingsFromStoreAsync()
        {
            _logger.LogInformation("SettingsService: Loading settings from JSON store...");

            // AssetDirectories
            var assetPref = await _settingsStore.GetAsync("AssetDirectories");
            _cache.AssetDirectories = ParseDirectoriesValue(assetPref);
            _logger.LogInformation("SettingsService: Loaded AssetDirectories count (parsed): {Count}", _cache.AssetDirectories.Count);
            await NormalizeAndPersistIfNeededAsync("AssetDirectories", assetPref, _cache.AssetDirectories);

            // ImageDirectories
            var imagePref = await _settingsStore.GetAsync("ImageDirectories");
            _cache.ImageDirectories = ParseDirectoriesValue(imagePref);
            _logger.LogInformation("SettingsService: Loaded ImageDirectories count (parsed): {Count}", _cache.ImageDirectories.Count);
            await NormalizeAndPersistIfNeededAsync("ImageDirectories", imagePref, _cache.ImageDirectories);

            // ProjectDirectories
            var projectPref = await _settingsStore.GetAsync("ProjectDirectories");
            _cache.ProjectDirectories = ParseDirectoriesValue(projectPref);
            _logger.LogInformation("SettingsService: Loaded ProjectDirectories count (parsed): {Count}", _cache.ProjectDirectories.Count);
            await NormalizeAndPersistIfNeededAsync("ProjectDirectories", projectPref, _cache.ProjectDirectories);

            // CodeDirectories
            var codePref = await _settingsStore.GetAsync("CodeDirectories");
            _cache.CodeDirectories = ParseDirectoriesValue(codePref);
            _logger.LogInformation("SettingsService: Loaded CodeDirectories count (parsed): {Count}", _cache.CodeDirectories.Count);
            await NormalizeAndPersistIfNeededAsync("CodeDirectories", codePref, _cache.CodeDirectories);

            // Code save output directory (migrated from Export.OutputDirectory)
            try
            {
                // Prefer new key used by Preferences Code > Save
                var codeSaveDir = await _settingsStore.GetAsync("Code.Save.OutputDirectory");
                if (string.IsNullOrWhiteSpace(codeSaveDir))
                {
                    // Fallback to legacy Export key for migration
                    codeSaveDir = await _settingsStore.GetAsync("Export.OutputDirectory");
                    if (!string.IsNullOrWhiteSpace(codeSaveDir))
                    {
                        // Persist into new key for future reads
                        try { await _settingsStore.UpsertAsync("Code.Save.OutputDirectory", codeSaveDir); } catch { }
                    }
                }
                _cache.CodeSaveOutputDirectory = codeSaveDir ?? string.Empty;
                _logger.LogInformation("SettingsService: Loaded Code.Save.OutputDirectory: {Dir}", _cache.CodeSaveOutputDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load Code.Save.OutputDirectory. Using empty string.");
                _cache.CodeSaveOutputDirectory = string.Empty;
            }

            // Code export format (migrated from Export.CodeFormat)
            try
            {
                var codeFmt = await _settingsStore.GetAsync("Code.Save.Format");
                if (string.IsNullOrWhiteSpace(codeFmt))
                {
                    codeFmt = await _settingsStore.GetAsync("Export.CodeFormat");
                }
                if (!string.IsNullOrWhiteSpace(codeFmt))
                {
                    _cache.CodeExportFormat = codeFmt;
                }
                else
                {
                    _cache.CodeExportFormat = "Json";
                }
                _logger.LogInformation("SettingsService: Loaded Code.Save.Format: {Fmt}", _cache.CodeExportFormat);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load Code.Save.Format. Using default Json.");
                _cache.CodeExportFormat = "Json";
            }
            // Color preferences (Scratchpad Editor)
            try
            {
                var scratchpadColor = await _settingsStore.GetAsync("Color.ScratchpadEditorColor");
                if (!string.IsNullOrWhiteSpace(scratchpadColor))
                {
                    _cache.ScratchpadEditorColor = scratchpadColor;
                }
                else
                {
                    // keep default from model
                }
                _logger.LogInformation("SettingsService: Loaded Color.ScratchpadEditorColor: {Color}", _cache.ScratchpadEditorColor);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load Color.ScratchpadEditorColor. Using default.");
            }

            // Overlay tint preferences
            try
            {
                var overlayColor = await _settingsStore.GetAsync("Color.OverlayTintColor");
                if (!string.IsNullOrWhiteSpace(overlayColor))
                {
                    _cache.OverlayTintColor = overlayColor;
                }
                _logger.LogInformation("SettingsService: Loaded Color.OverlayTintColor: {Color}", _cache.OverlayTintColor);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load Color.OverlayTintColor. Using default.");
            }

            try
            {
                var overlayOpacity = await _settingsStore.GetAsync("Color.OverlayTintOpacity");
                if (!string.IsNullOrWhiteSpace(overlayOpacity) &&
                    double.TryParse(overlayOpacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity) &&
                    opacity >= 0 && opacity <= 1)
                {
                    _cache.OverlayTintOpacity = opacity;
                }
                _logger.LogInformation("SettingsService: Loaded Color.OverlayTintOpacity: {Opacity}", _cache.OverlayTintOpacity);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load Color.OverlayTintOpacity. Using default.");
            }

            try
            {
                var overlayLuminosity = await _settingsStore.GetAsync("Color.OverlayTintLuminosityOpacity");
                if (!string.IsNullOrWhiteSpace(overlayLuminosity) &&
                    double.TryParse(overlayLuminosity, NumberStyles.Float, CultureInfo.InvariantCulture, out var luminosity) &&
                    luminosity >= 0 && luminosity <= 1)
                {
                    _cache.OverlayTintLuminosityOpacity = luminosity;
                }
                _logger.LogInformation("SettingsService: Loaded Color.OverlayTintLuminosityOpacity: {Luminosity}", _cache.OverlayTintLuminosityOpacity);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load Color.OverlayTintLuminosityOpacity. Using default.");
            }

            try
            {
                var transitionMs = await _settingsStore.GetAsync("Color.OverlayTintTransitionMs");
                if (!string.IsNullOrWhiteSpace(transitionMs) &&
                    int.TryParse(transitionMs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMs) &&
                    parsedMs >= 0)
                {
                    _cache.OverlayTintTransitionDurationMs = parsedMs;
                }
                _logger.LogInformation("SettingsService: Loaded Color.OverlayTintTransitionMs: {Ms}", _cache.OverlayTintTransitionDurationMs);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load Color.OverlayTintTransitionMs. Using default.");
            }

            // Last selected snippet id
            try
            {
                var lastIdStr = await _settingsStore.GetAsync("Code.LastSelectedSnippetId");
                if (!string.IsNullOrWhiteSpace(lastIdStr) && Guid.TryParse(lastIdStr, out var lid))
                {
                    _cache.LastSelectedSnippetId = lid;
                }
                else
                {
                    _cache.LastSelectedSnippetId = Guid.Empty;
                }
            }
            catch { _cache.LastSelectedSnippetId = Guid.Empty; }

            if (Enum.TryParse<ElementTheme>(await _settingsStore.GetAsync("AppTheme"), out var theme))
            {
                _cache.AppTheme = theme;
            }
            else
            {
                _cache.AppTheme = ElementTheme.Default;
            }
            _logger.LogInformation("SettingsService: Loaded AppTheme: {Theme}", _cache.AppTheme);

            if (Enum.TryParse<BackdropType>(await _settingsStore.GetAsync("AppBackdropType"), out var backdrop))
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
                var modeStr = await _settingsStore.GetAsync("AssetDisplayMode");
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
                var showMetaStr = await _settingsStore.GetAsync("ShowAssetMetadata");
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
                var forceFullStr = await _settingsStore.GetAsync("Scan.ForceFull");
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
                var modeStr = await _settingsStore.GetAsync("MenuDisplayMode");
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

            // Asset Filters (customizable filter groups)
            try
            {
                var filtersJson = await _settingsStore.GetAsync("AssetFilters");
                if (!string.IsNullOrWhiteSpace(filtersJson) && LooksLikeJsonArray(filtersJson))
                {
                    try
                    {
                        var filters = JsonSerializer.Deserialize<List<Pivot.Models.CustomFilter>>(filtersJson);
                        if (filters != null && filters.Count > 0)
                        {
                            _cache.AssetFilters = filters;
                        }
                        else
                        {
                            // initialize defaults if deserialization yields nothing
                            _cache.AssetFilters = GetDefaultAssetFilters();
                            await _settingsStore.UpsertAsync("AssetFilters", JsonSerializer.Serialize(_cache.AssetFilters));
                        }
                    }
                    catch
                    {
                        _cache.AssetFilters = GetDefaultAssetFilters();
                        await _settingsStore.UpsertAsync("AssetFilters", JsonSerializer.Serialize(_cache.AssetFilters));
                    }
                }
                else
                {
                    _cache.AssetFilters = GetDefaultAssetFilters();
                    await _settingsStore.UpsertAsync("AssetFilters", JsonSerializer.Serialize(_cache.AssetFilters));
                }
                _logger.LogInformation("SettingsService: Loaded AssetFilters: {Count}", _cache.AssetFilters.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load AssetFilters. Using defaults.");
                _cache.AssetFilters = GetDefaultAssetFilters();
            }

            // Code Filters (customizable filter groups)
            try
            {
                var codeFiltersJson = await _settingsStore.GetAsync("CodeFilters");
                if (!string.IsNullOrWhiteSpace(codeFiltersJson) && LooksLikeJsonArray(codeFiltersJson))
                {
                    try
                    {
                        var filters = JsonSerializer.Deserialize<List<Pivot.Models.CustomFilter>>(codeFiltersJson);
                        if (filters != null && filters.Count > 0)
                        {
                            _cache.CodeFilters = filters;
                        }
                        else
                        {
                            // initialize defaults if deserialization yields nothing
                            _cache.CodeFilters = GetDefaultCodeFilters();
                            await _settingsStore.UpsertAsync("CodeFilters", JsonSerializer.Serialize(_cache.CodeFilters));
                        }
                    }
                    catch
                    {
                        _cache.CodeFilters = GetDefaultCodeFilters();
                        await _settingsStore.UpsertAsync("CodeFilters", JsonSerializer.Serialize(_cache.CodeFilters));
                    }
                }
                else
                {
                    _cache.CodeFilters = GetDefaultCodeFilters();
                    await _settingsStore.UpsertAsync("CodeFilters", JsonSerializer.Serialize(_cache.CodeFilters));
                }
                _logger.LogInformation("SettingsService: Loaded CodeFilters: {Count}", _cache.CodeFilters.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load CodeFilters. Using defaults.");
                _cache.CodeFilters = GetDefaultCodeFilters();
            }

            // Code Categories (groups for Code tab navigation)
            try
            {
                var codeCategoriesJson = await _settingsStore.GetAsync("CodeCategories");
                if (!string.IsNullOrWhiteSpace(codeCategoriesJson) && codeCategoriesJson.TrimStart().StartsWith("["))
                {
                    try
                    {
                        var cats = JsonSerializer.Deserialize<List<Pivot.Models.CodeCategory>>(codeCategoriesJson);
                        if (cats != null)
                        {
                            _cache.CodeCategories = cats;
                        }
                        else
                        {
                            InitializeDefaultCodeCategories();
                        }
                    }
                    catch
                    {
                        InitializeDefaultCodeCategories();
                    }
                }
                else
                {
                    InitializeDefaultCodeCategories();
                }
                _logger.LogInformation("SettingsService: Loaded CodeCategories: {Count}", _cache.CodeCategories?.Count ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load CodeCategories. Using defaults.");
                InitializeDefaultCodeCategories();
            }

            // Visible filters per tab
            try
            {
                var visibleJson = await _settingsStore.GetAsync("VisibleFiltersByTab");
                if (!string.IsNullOrWhiteSpace(visibleJson))
                {
                    try
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, List<Guid>>>(visibleJson);
                        if (dict != null)
                        {
                            _cache.VisibleFiltersByTab = dict;
                        }
                        else
                        {
                            // default: make all asset filters visible on Asset tab
                            _cache.VisibleFiltersByTab = new Dictionary<string, List<Guid>> { { "Asset", _cache.AssetFilters.Select(f => f.Id).ToList() } };
                            await _settingsStore.UpsertAsync("VisibleFiltersByTab", JsonSerializer.Serialize(_cache.VisibleFiltersByTab));
                        }
                    }
                    catch
                    {
                        _cache.VisibleFiltersByTab = new Dictionary<string, List<Guid>> { { "Asset", _cache.AssetFilters.Select(f => f.Id).ToList() } };
                        await _settingsStore.UpsertAsync("VisibleFiltersByTab", JsonSerializer.Serialize(_cache.VisibleFiltersByTab));
                    }
                }
                else
                {
                    _cache.VisibleFiltersByTab = new Dictionary<string, List<Guid>> { { "Asset", _cache.AssetFilters.Select(f => f.Id).ToList() } };
                    await _settingsStore.UpsertAsync("VisibleFiltersByTab", JsonSerializer.Serialize(_cache.VisibleFiltersByTab));
                }
                _logger.LogInformation("SettingsService: Loaded VisibleFiltersByTab for {Count} tabs", _cache.VisibleFiltersByTab.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load VisibleFiltersByTab. Using defaults.");
                _cache.VisibleFiltersByTab = new Dictionary<string, List<Guid>> { { "Asset", _cache.AssetFilters.Select(f => f.Id).ToList() } };
            }

            // Selected filters per tab (restore selection state)
            try
            {
                var selJson = await _settingsStore.GetAsync("SelectedFiltersByTab");
                if (!string.IsNullOrWhiteSpace(selJson))
                {
                    try
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, List<Guid>>>(selJson);
                        if (dict != null)
                        {
                            _cache.SelectedFiltersByTab = dict;
                        }
                        else
                        {
                            _cache.SelectedFiltersByTab = new Dictionary<string, List<Guid>>();
                            await _settingsStore.UpsertAsync("SelectedFiltersByTab", JsonSerializer.Serialize(_cache.SelectedFiltersByTab));
                        }
                    }
                    catch
                    {
                        _cache.SelectedFiltersByTab = new Dictionary<string, List<Guid>>();
                        await _settingsStore.UpsertAsync("SelectedFiltersByTab", JsonSerializer.Serialize(_cache.SelectedFiltersByTab));
                    }
                }
                else
                {
                    _cache.SelectedFiltersByTab = new Dictionary<string, List<Guid>>();
                    await _settingsStore.UpsertAsync("SelectedFiltersByTab", JsonSerializer.Serialize(_cache.SelectedFiltersByTab));
                }
                _logger.LogInformation("SettingsService: Loaded SelectedFiltersByTab for {Count} tabs", _cache.SelectedFiltersByTab.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load SelectedFiltersByTab. Using defaults.");
                _cache.SelectedFiltersByTab = new Dictionary<string, List<Guid>>();
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
                    await _settingsStore.UpsertAsync(key, normalized);
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
            _cache.AppTheme = theme;
            await _settingsStore.UpsertAsync("AppTheme", theme.ToString());
        }

        public BackdropType GetBackdropType() => _cache.AppBackdropType;

        public async Task SetBackdropType(BackdropType type)
        {
            _cache.AppBackdropType = type;
            await _settingsStore.UpsertAsync("AppBackdropType", type.ToString());
        }

        // Export output directory accessors
        // Backwards-compatible accessor: returns the configured code save output directory.
        public string GetExportOutputDirectory() => _cache.CodeSaveOutputDirectory ?? string.Empty;

        // Persist the code save output directory under the new key used by Preferences > Code > Save.
        public async Task SetExportOutputDirectoryAsync(string path)
        {
            _cache.CodeSaveOutputDirectory = path ?? string.Empty;
            try
            {
                await _settingsStore.UpsertAsync("Code.Save.OutputDirectory", _cache.CodeSaveOutputDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist Code.Save.OutputDirectory.");
            }
        }

        public Pivot.Models.CodeExportFormat GetCodeExportFormat()
        {
            try
            {
                if (Enum.TryParse<Pivot.Models.CodeExportFormat>(_cache.CodeExportFormat, out var fmt)) return fmt;
            }
            catch { }
            return Pivot.Models.CodeExportFormat.Json;
        }

        public async Task SetCodeExportFormatAsync(Pivot.Models.CodeExportFormat format)
        {
            _cache.CodeExportFormat = format.ToString();
            try
            {
                // Persist under the Code.Save namespace
                await _settingsStore.UpsertAsync("Code.Save.Format", _cache.CodeExportFormat);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist Export.CodeFormat.");
            }
        }

        public Guid GetLastSelectedSnippetId()
        {
            try { return _cache.LastSelectedSnippetId; } catch { return Guid.Empty; }
        }

        public async Task SetLastSelectedSnippetIdAsync(Guid id)
        {
            _cache.LastSelectedSnippetId = id;
            try
            {
                await _settingsStore.UpsertAsync("Code.LastSelectedSnippetId", id.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist Code.LastSelectedSnippetId.");
            }
        }

        // Asset 表示モード/メタ表示 設定
        public AssetDisplayMode GetAssetDisplayMode() => _cache.AssetDisplayMode;

        public async Task SetAssetDisplayModeAsync(AssetDisplayMode mode)
        {
            _cache.AssetDisplayMode = mode;
            await _settingsStore.UpsertAsync("AssetDisplayMode", mode.ToString());
        }

        public bool GetShowAssetMetadata() => _cache.ShowAssetMetadata;

        public async Task SetShowAssetMetadataAsync(bool show)
        {
            _cache.ShowAssetMetadata = show;
            await _settingsStore.UpsertAsync("ShowAssetMetadata", show.ToString());
        }

        // スキャン戦略設定
        public bool GetForceFullScan() => _cache.ForceFullScan;

        public async Task SetForceFullScanAsync(bool force)
        {
            _cache.ForceFullScan = force;
            await _settingsStore.UpsertAsync("Scan.ForceFull", force.ToString());
        }

        public async Task AddDirectoryAsync(DirectoryCategory category, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            string key = category switch
            {
                DirectoryCategory.Asset => "AssetDirectories",
                DirectoryCategory.Image => "ImageDirectories",
                DirectoryCategory.Project => "ProjectDirectories",
                DirectoryCategory.Code => "CodeDirectories",
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };

            var list = category switch
            {
                DirectoryCategory.Asset => _cache.AssetDirectories,
                DirectoryCategory.Image => _cache.ImageDirectories,
                DirectoryCategory.Project => _cache.ProjectDirectories,
                DirectoryCategory.Code => _cache.CodeDirectories,
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };

            _logger.LogDebug("[SettingsService] AddDirectoryAsync: Category={Category}, Path='{Path}', Key='{Key}'", category, path, key);
            _logger.LogDebug("[SettingsService] AddDirectoryAsync: Current cache list for {Key}: {List}", key, string.Join(", ", list));

            if (!list.Contains(path))
            {
                list.Add(path);
                string serializedList = JsonSerializer.Serialize(list);
                _logger.LogDebug("[SettingsService] AddDirectoryAsync: New serialized list for {Key}: {SerializedList}", key, serializedList);
                await _settingsStore.UpsertAsync(key, serializedList);
                _logger.LogDebug("[SettingsService] AddDirectoryAsync: Successfully upserted for {Key}.", key);
                // ディレクトリ追加メッセージを送信
                _messenger.Send(new DirectoryChangedMessage(new DirectoryChangedMessageData
                {
                    Category = category,
                    Path = path,
                    Type = DirectoryChangedMessageData.ChangeType.Added
                }));
            }
            else
            {
                _logger.LogDebug("[SettingsService] AddDirectoryAsync: Path '{Path}' already exists in cache for {Key}.", path, key);
            }
        }

        public async Task RemoveDirectoryAsync(DirectoryCategory category, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            string key = category switch
            {
                DirectoryCategory.Asset => "AssetDirectories",
                DirectoryCategory.Image => "ImageDirectories",
                DirectoryCategory.Project => "ProjectDirectories",
                DirectoryCategory.Code => "CodeDirectories",
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };

            var list = category switch
            {
                DirectoryCategory.Asset => _cache.AssetDirectories,
                DirectoryCategory.Image => _cache.ImageDirectories,
                DirectoryCategory.Project => _cache.ProjectDirectories,
                DirectoryCategory.Code => _cache.CodeDirectories,
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };

            _logger.LogDebug("[SettingsService] RemoveDirectoryAsync: Category={Category}, Path='{Path}', Key='{Key}'", category, path, key);
            _logger.LogDebug("[SettingsService] RemoveDirectoryAsync: Current cache list for {Key}: {List}", key, string.Join(", ", list));

            if (list.Remove(path))
            {
                string serializedList = JsonSerializer.Serialize(list);
                _logger.LogDebug("[SettingsService] RemoveDirectoryAsync: New serialized list for {Key}: {SerializedList}", key, serializedList);
                await _settingsStore.UpsertAsync(key, serializedList);
                _logger.LogDebug("[SettingsService] RemoveDirectoryAsync: Successfully upserted after removal for {Key}.", key);
                // ディレクトリ削除メッセージを送信
                _messenger.Send(new DirectoryChangedMessage(new DirectoryChangedMessageData
                {
                    Category = category,
                    Path = path,
                    Type = DirectoryChangedMessageData.ChangeType.Removed
                }));
            }
            else
            {
                _logger.LogDebug("[SettingsService] RemoveDirectoryAsync: Path '{Path}' not found in cache for {Key}.", path, key);
            }
        }

        public MenuDisplayMode GetMenuDisplayMode() => _cache.MenuDisplayMode;

        public async Task SetMenuDisplayModeAsync(MenuDisplayMode mode)
        {
            _cache.MenuDisplayMode = mode;
            await _settingsStore.UpsertAsync("MenuDisplayMode", mode.ToString());
        }

        private List<Pivot.Models.CustomFilter> GetDefaultAssetFilters()
        {
            return new List<Pivot.Models.CustomFilter>
            {
                new Pivot.Models.CustomFilter { Name = "3D", AllowedExtensions = new List<string> { ".obj", ".fbx", ".gltf", ".glb", ".dae" }, IsBuiltIn = true, SortOrder = 0 },
                new Pivot.Models.CustomFilter { Name = "Images", AllowedExtensions = new List<string> { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tga", ".tif", ".tiff" }, IsBuiltIn = true, SortOrder = 1 },
                new Pivot.Models.CustomFilter { Name = "Video", AllowedExtensions = new List<string> { ".mp4", ".mov", ".avi", ".mkv", ".webm" }, IsBuiltIn = true, SortOrder = 2 },
                new Pivot.Models.CustomFilter { Name = "Audio", AllowedExtensions = new List<string> { ".mp3", ".wav", ".ogg", ".flac", ".aac" }, IsBuiltIn = true, SortOrder = 3 },
                new Pivot.Models.CustomFilter { Name = "Other", AllowedExtensions = new List<string>(), IsBuiltIn = true, SortOrder = 4 }
            };
        }

        private List<Pivot.Models.CustomFilter> GetDefaultCodeFilters()
        {
            return new List<Pivot.Models.CustomFilter>
            {
                new Pivot.Models.CustomFilter { Name = "C#", IsBuiltIn=true },
                new Pivot.Models.CustomFilter { Name = "Python", IsBuiltIn=true },
                new Pivot.Models.CustomFilter { Name = "JavaScript", IsBuiltIn=true },
                new Pivot.Models.CustomFilter { Name = "HTML", IsBuiltIn=true },
                new Pivot.Models.CustomFilter { Name = "CSS", IsBuiltIn=true },
                new Pivot.Models.CustomFilter { Name = "SQL", IsBuiltIn=true },
                new Pivot.Models.CustomFilter { Name = "Markdown", IsBuiltIn=true },
                new Pivot.Models.CustomFilter { Name = "Other", IsBuiltIn=true }
            };
        }

        private void InitializeDefaultCodeCategories()
        {
            try
            {
                // Default single category that contains all current code filters
                var allFilterIds = (_cache.CodeFilters ?? new List<Pivot.Models.CustomFilter>()).Select(f => f.Id).ToList();
                _cache.CodeCategories = new List<Pivot.Models.CodeCategory>
                {
                    new Pivot.Models.CodeCategory { Name = "Languages", FilterIds = allFilterIds, SortOrder = 0 }
                };
                // Persist
                _settingsStore.UpsertAsync("CodeCategories", JsonSerializer.Serialize(_cache.CodeCategories)).ConfigureAwait(false);
            }
            catch { _cache.CodeCategories = new List<Pivot.Models.CodeCategory>(); }
        }

        // AssetFilters accessors
        public List<Pivot.Models.CustomFilter> GetAssetFilters() => _cache.AssetFilters;

        public async Task SetAssetFiltersAsync(List<Pivot.Models.CustomFilter> filters)
        {
            _cache.AssetFilters = filters ?? new List<Pivot.Models.CustomFilter>();
            try
            {
                await _settingsStore.UpsertAsync("AssetFilters", JsonSerializer.Serialize(_cache.AssetFilters));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist AssetFilters.");
            }
        }

        // CodeFilters accessors
        public List<Pivot.Models.CustomFilter> GetCodeFilters() => _cache.CodeFilters;

        public async Task SetCodeFiltersAsync(List<Pivot.Models.CustomFilter> filters)
        {
            _cache.CodeFilters = filters ?? new List<Pivot.Models.CustomFilter>();
            try
            {
                await _settingsStore.UpsertAsync("CodeFilters", JsonSerializer.Serialize(_cache.CodeFilters));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist CodeFilters.");
            }
        }

        // CodeCategories accessors
        public List<Pivot.Models.CodeCategory> GetCodeCategories() => _cache.CodeCategories ?? new List<Pivot.Models.CodeCategory>();

        public async Task SetCodeCategoriesAsync(List<Pivot.Models.CodeCategory> categories)
        {
            _cache.CodeCategories = categories ?? new List<Pivot.Models.CodeCategory>();
            try
            {
                await _settingsStore.UpsertAsync("CodeCategories", JsonSerializer.Serialize(_cache.CodeCategories));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist CodeCategories.");
            }
        }

        public string GetFilterNameById(Guid filterId)
        {
            // Check AssetFilters first
            var assetFilter = _cache.AssetFilters.FirstOrDefault(f => f.Id == filterId);
            if (assetFilter != null) return assetFilter.Name;

            // If not found in AssetFilters, check CodeFilters
            var codeFilter = _cache.CodeFilters.FirstOrDefault(f => f.Id == filterId);
            if (codeFilter != null) return codeFilter.Name;

            return string.Empty; // Return empty if not found in either
        }

        // Visible filters per tab accessors
        public List<Guid> GetVisibleFiltersForTab(string tabId)
        {
            if (string.IsNullOrWhiteSpace(tabId)) return new List<Guid>();
            if (_cache.VisibleFiltersByTab != null && _cache.VisibleFiltersByTab.TryGetValue(tabId, out var list)) return list;
            return new List<Guid>();
        }

        public async Task SetVisibleFiltersForTabAsync(string tabId, List<Guid> filterIds)
        {
            if (string.IsNullOrWhiteSpace(tabId)) return;
            if (_cache.VisibleFiltersByTab == null) _cache.VisibleFiltersByTab = new Dictionary<string, List<Guid>>();
            _cache.VisibleFiltersByTab[tabId] = filterIds ?? new List<Guid>();
            try
            {
                await _settingsStore.UpsertAsync("VisibleFiltersByTab", JsonSerializer.Serialize(_cache.VisibleFiltersByTab));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist VisibleFiltersByTab.");
            }
        }

        public List<Guid> GetSelectedFiltersForTab(string tabId)
        {
            if (string.IsNullOrWhiteSpace(tabId)) return new List<Guid>();
            if (_cache.SelectedFiltersByTab != null && _cache.SelectedFiltersByTab.TryGetValue(tabId, out var list)) return list;
            return new List<Guid>();
        }

        public async Task SetSelectedFiltersForTabAsync(string tabId, List<Guid> filterIds)
        {
            if (string.IsNullOrWhiteSpace(tabId)) return;
            if (_cache.SelectedFiltersByTab == null) _cache.SelectedFiltersByTab = new Dictionary<string, List<Guid>>();
            _cache.SelectedFiltersByTab[tabId] = filterIds ?? new List<Guid>();
            try
            {
                await _settingsStore.UpsertAsync("SelectedFiltersByTab", JsonSerializer.Serialize(_cache.SelectedFiltersByTab));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist SelectedFiltersByTab.");
            }
        }

        // Color preferences accessors
        public string GetScratchpadEditorColor() => _cache.ScratchpadEditorColor ?? "#FFFFFF";

        public async Task SetScratchpadEditorColorAsync(string color)
        {
            _cache.ScratchpadEditorColor = color ?? "#FFFFFF";
            try
            {
                await _settingsStore.UpsertAsync("Color.ScratchpadEditorColor", _cache.ScratchpadEditorColor);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist Color.ScratchpadEditorColor.");
            }
        }
        // Overlay tint accessors
        public string GetOverlayTintColor() => _cache.OverlayTintColor ?? "#0000FF";

        public async Task SetOverlayTintColorAsync(string color)
        {
            _cache.OverlayTintColor = color ?? "#0000FF";
            try
            {
                await _settingsStore.UpsertAsync("Color.OverlayTintColor", _cache.OverlayTintColor);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist Color.OverlayTintColor.");
            }
        }
        public double GetOverlayTintOpacity() => _cache.OverlayTintOpacity;

        public async Task SetOverlayTintOpacityAsync(double value)
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            _cache.OverlayTintOpacity = clamped;
            try
            {
                await _settingsStore.UpsertAsync("Color.OverlayTintOpacity", clamped.ToString("G", CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist Color.OverlayTintOpacity.");
            }
        }

        public double GetOverlayTintLuminosityOpacity() => _cache.OverlayTintLuminosityOpacity;

        public async Task SetOverlayTintLuminosityOpacityAsync(double value)
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            _cache.OverlayTintLuminosityOpacity = clamped;
            try
            {
                await _settingsStore.UpsertAsync("Color.OverlayTintLuminosityOpacity", clamped.ToString("G", CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist Color.OverlayTintLuminosityOpacity.");
            }
        }

        public int GetOverlayTintTransitionDurationMs() => _cache.OverlayTintTransitionDurationMs;

        public async Task SetOverlayTintTransitionDurationMsAsync(int milliseconds)
        {
            var clamped = Math.Max(0, milliseconds);
            _cache.OverlayTintTransitionDurationMs = clamped;
            try
            {
                await _settingsStore.UpsertAsync("Color.OverlayTintTransitionMs", clamped.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist Color.OverlayTintTransitionMs.");
            }
        }

    }
}
