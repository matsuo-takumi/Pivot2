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
using Pivot.ViewModels;

namespace Pivot.Services
{
    public class SettingsService
    {
        // private readonly IConfiguration _configuration; // 使用しないため削除
        private readonly ILogger<SettingsService> _logger;
        private readonly MetadataService _metadataService;
        private readonly IMessenger _messenger;
        private readonly ISettingsStore _settingsStore;
        private readonly DirectorySettingsService _directorySettings;
        private readonly ThemeSettingsService _themeSettings;

        // In-memory cache to avoid sync-over-async and improve UI responsiveness
        private UserSettings _cache; // Settingsをメモリにキャッシュ

        public SettingsService(
            ILogger<SettingsService> logger,
            MetadataService metadataService,
            IMessenger messenger, 
            DirectorySettingsService directorySettings,
            ThemeSettingsService themeSettings,
            ISettingsStore? settingsStore = null)
        {
            _logger = logger;
            _metadataService = metadataService;
            _messenger = messenger;
            _directorySettings = directorySettings;
            _themeSettings = themeSettings;
            _settingsStore = settingsStore ?? new JsonSettingsStore();
            _cache = new UserSettings(); // 初期キャッシュ
        }

        // DB依存を排除し、JSON設定ストアに移行

        public async Task InitializeAsync()
        {
            _logger.LogInformation("SettingsService: Initializing...");
            await _settingsStore.InitializeAsync();
            // Initialize sub-services
            await _directorySettings.LoadAsync();
            await _themeSettings.LoadAsync();
            
            await LoadSettingsFromStoreAsync();
            
            // Sync initial state to cache
            SyncDirectoriesFromService();
            SyncThemeFromService();
            
            _logger.LogInformation("SettingsService: Initialization complete.");
        }

        private void SyncDirectoriesFromService()
        {
            _cache.AssetDirectories = _directorySettings.AssetDirectories.ToList();
            _cache.ImageDirectories = _directorySettings.ImageDirectories.ToList();
            _cache.ProjectDirectories = _directorySettings.ProjectDirectories.ToList();
            _cache.CodeDirectories = _directorySettings.CodeDirectories.ToList();
            _cache.CodeSaveOutputDirectory = _directorySettings.CodeSaveOutputDirectory;
        }

        private void SyncThemeFromService()
        {
            _cache.AppTheme = _themeSettings.AppTheme;
            _cache.AppBackdropType = _themeSettings.AppBackdropType;
            _cache.OverlayTintColor = _themeSettings.OverlayTintColor;
            _cache.OverlayTintOpacity = _themeSettings.OverlayTintOpacity;
            _cache.OverlayTintLuminosityOpacity = _themeSettings.OverlayTintLuminosityOpacity;
            _cache.OverlayTintTransitionDurationMs = _themeSettings.OverlayTintTransitionDurationMs;
            
            _cache.PreferredTextColorTemplate = _themeSettings.PreferredTextColorTemplate;
            _cache.DominantColor = _themeSettings.DominantColor;
            _cache.DominantVariation = _themeSettings.DominantVariation;
            _cache.DominantGenerateAccent = _themeSettings.DominantGenerateAccent;
            _cache.DominantAccentStrength = _themeSettings.DominantAccentStrength;
            
            _cache.RandomSeed = _themeSettings.RandomSeed;
            _cache.RandomnessLevel = _themeSettings.RandomnessLevel;
            _cache.RandomSaturationMin = _themeSettings.RandomSaturationMin;
            _cache.RandomSaturationMax = _themeSettings.RandomSaturationMax;
            _cache.RandomBrightnessMin = _themeSettings.RandomBrightnessMin;
            _cache.RandomBrightnessMax = _themeSettings.RandomBrightnessMax;
            _cache.RandomAllowExtreme = _themeSettings.RandomAllowExtreme;
            
            _cache.TextColorOverrides = new Dictionary<string, string>(_themeSettings.TextColorOverrides);
            _cache.IsTextColorCustomizationEnabled = _themeSettings.IsTextColorCustomizationEnabled;
            
            _cache.ImageSelectionColor = _themeSettings.ImageSelectionColor;
            _cache.ImageSelectionOpacity = _themeSettings.ImageSelectionOpacity;
            _cache.ImageSelectionBorderThickness = _themeSettings.ImageSelectionBorderThickness;
            _cache.ImageDragSelectionColor = _themeSettings.ImageDragSelectionColor;
            _cache.ImageDragSelectionOpacity = _themeSettings.ImageDragSelectionOpacity;
        }

        private async Task LoadSettingsFromStoreAsync()
        {
            _logger.LogInformation("SettingsService: Loading settings from JSON store...");

            // AssetDirectories, ImageDirectories, ProjectDirectories, CodeDirectories, OutputDirectory, CodeFormat
            // are now handled by DirectorySettingsService and initialized in InitializeAsync.
            // SyncDirectoriesFromService() ensures _cache is populated.

            // Code save output directory
            CodeSaveOutputDirectoryLogic_Migrated_To_Service();
            
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
            // Overlay, TextColor, and Theme settings are now loaded by ThemeSettingsService
            // and synced via SyncThemeFromService().





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

            // Viewport camera gesture preset
            try
            {
                var gestureStr = await _settingsStore.GetAsync("Viewport.CameraGesture");
                if (Enum.TryParse<CameraGesturePreset>(gestureStr, out var gesture))
                {
                    _cache.ViewportCameraGesture = gesture;
                }
                else
                {
                    _cache.ViewportCameraGesture = CameraGesturePreset.Maya;
                }
                _logger.LogInformation("SettingsService: Loaded ViewportCameraGesture: {Gesture}", _cache.ViewportCameraGesture);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load ViewportCameraGesture. Using default (Maya).");
                _cache.ViewportCameraGesture = CameraGesturePreset.Maya;
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

        private static bool LooksLikeJsonObject(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var v = value.TrimStart();
            return v.Length > 0 && v[0] == '{';
        }

        private void CodeSaveOutputDirectoryLogic_Migrated_To_Service()
        {
             // Placeholder to indicate separation
        }

        // Synchronous getters now use in-memory cache
        public UserSettings GetUserSettings()
        {
            // Ensure latest directory state is reflected in cache
            // (Note: This creates new lists every call, which is safer but might be slightly slower. 
            // Given low frequency, it's acceptable.)
            SyncDirectoriesFromService();
            SyncThemeFromService();
            _logger.LogDebug("SettingsService: GetUserSettings called. Current AssetDirectories count: {Count}", _cache.AssetDirectories.Count);
            return _cache;
        }

        public ElementTheme GetTheme() => _cache.AppTheme;

        public async Task SetTheme(ElementTheme theme)
        {
            await _themeSettings.SetThemeAsync(theme);
            SyncThemeFromService();
        }

        public BackdropType GetBackdropType() => _cache.AppBackdropType;

        public async Task SetBackdropType(BackdropType type)
        {
            await _themeSettings.SetBackdropTypeAsync(type);
            SyncThemeFromService();
        }

        // Export output directory accessors
        // Backwards-compatible accessor: returns the configured code save output directory.
        public string GetExportOutputDirectory() => _cache.CodeSaveOutputDirectory ?? string.Empty;

        // Persist the code save output directory under the new key used by Preferences > Code > Save.
        public async Task SetExportOutputDirectoryAsync(string path)
        {
             // Delegate to new service
             await _directorySettings.SetCodeSaveOutputDirectoryAsync(path);
             SyncDirectoriesFromService();
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
            await _directorySettings.AddDirectoryAsync(category, path);
            SyncDirectoriesFromService();
        }
        
        public async Task RemoveDirectoryAsync(DirectoryCategory category, string path)
        {
            await _directorySettings.RemoveDirectoryAsync(category, path);
            SyncDirectoriesFromService();
        }

        public MenuDisplayMode GetMenuDisplayMode() => _cache.MenuDisplayMode;

        public async Task SetMenuDisplayModeAsync(MenuDisplayMode mode)
        {
            _cache.MenuDisplayMode = mode;
            await _settingsStore.UpsertAsync("MenuDisplayMode", mode.ToString());
        }

        private List<Pivot.Models.CustomFilter> GetDefaultAssetFilters()
        {
            // デフォルトタグなし - ユーザーが自分で作成
            return new List<Pivot.Models.CustomFilter>();
        }

        private List<Pivot.Models.CustomFilter> GetDefaultCodeFilters()
        {
            // Default filters intentionally empty so only user-configured entries appear.
            return new List<Pivot.Models.CustomFilter>();
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
                _messenger?.Send(new Messages.CodeFiltersUpdatedMessage(_cache.CodeFilters));
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

        /// <summary>
        /// Updates a filter name by its ID and returns the old name.
        /// </summary>
        public async Task<string?> UpdateFilterNameAsync(Guid filterId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return null;

            try
            {
                var filter = _cache.CodeFilters.FirstOrDefault(f => f.Id == filterId);
                if (filter == null) return null;

                var oldName = filter.Name;
                filter.Name = newName.Trim();
                await SetCodeFiltersAsync(_cache.CodeFilters);
                return oldName;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to update filter name.");
                return null;
            }
        }

        /// <summary>
        /// Deletes a filter by its ID and returns the old name.
        /// </summary>
        public async Task<string?> DeleteFilterAsync(Guid filterId)
        {
            try
            {
                var filter = _cache.CodeFilters.FirstOrDefault(f => f.Id == filterId);
                if (filter == null) return null;

                var oldName = filter.Name;
                _cache.CodeFilters.Remove(filter);
                await SetCodeFiltersAsync(_cache.CodeFilters);
                return oldName;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to delete filter.");
                return null;
            }
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



        public TextColorTemplate GetTextColorTemplate() => _cache.PreferredTextColorTemplate;

        public async Task SetTextColorTemplateAsync(TextColorTemplate template)
        {
            await _themeSettings.SetTextColorTemplateAsync(template);
            SyncThemeFromService();
        }

        public string GetDominantColor() => _cache.DominantColor;

        public async Task SetDominantColorAsync(string hex)
        {
            await _themeSettings.SetDominantColorAsync(hex);
            SyncThemeFromService();
        }

        public double GetDominantVariation() => _cache.DominantVariation;

        public async Task SetDominantVariationAsync(double value)
        {
            await _themeSettings.SetDominantVariationAsync(value);
            SyncThemeFromService();
        }

        public bool GetDominantGenerateAccent() => _cache.DominantGenerateAccent;

        public async Task SetDominantGenerateAccentAsync(bool value)
        {
            await _themeSettings.SetDominantGenerateAccentAsync(value);
            SyncThemeFromService();
        }

        public double GetDominantAccentStrength() => _cache.DominantAccentStrength;

        public async Task SetDominantAccentStrengthAsync(double value)
        {
            await _themeSettings.SetDominantAccentStrengthAsync(value);
            SyncThemeFromService();
        }

        public int GetRandomSeed() => _cache.RandomSeed;

        public async Task SetRandomSeedAsync(int value)
        {
            await _themeSettings.SetRandomSeedAsync(value);
            SyncThemeFromService();
        }

        public double GetRandomnessLevel() => _cache.RandomnessLevel;

        public async Task SetRandomnessLevelAsync(double value)
        {
            await _themeSettings.SetRandomnessLevelAsync(value);
            SyncThemeFromService();
        }

        public double GetRandomSaturationMin() => _cache.RandomSaturationMin;

        public async Task SetRandomSaturationMinAsync(double value)
        {
            await _themeSettings.SetRandomSaturationMinAsync(value);
            SyncThemeFromService();
        }

        public double GetRandomSaturationMax() => _cache.RandomSaturationMax;

        public async Task SetRandomSaturationMaxAsync(double value)
        {
            await _themeSettings.SetRandomSaturationMaxAsync(value);
            SyncThemeFromService();
        }

        public double GetRandomBrightnessMin() => _cache.RandomBrightnessMin;

        public async Task SetRandomBrightnessMinAsync(double value)
        {
            await _themeSettings.SetRandomBrightnessMinAsync(value);
            SyncThemeFromService();
        }

        public double GetRandomBrightnessMax() => _cache.RandomBrightnessMax;

        public async Task SetRandomBrightnessMaxAsync(double value)
        {
            await _themeSettings.SetRandomBrightnessMaxAsync(value);
            SyncThemeFromService();
        }

        public bool GetRandomAllowExtreme() => _cache.RandomAllowExtreme;

        public async Task SetRandomAllowExtremeAsync(bool value)
        {
            await _themeSettings.SetRandomAllowExtremeAsync(value);
            SyncThemeFromService();
        }

        private async Task PersistSettingAsync(string key, string value)
        {
            try
            {
                await _settingsStore.UpsertAsync(key, value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist {Key}.", key);
            }
        }

        public string GetTextColorOverride(string key, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(key)) return defaultValue;
            if (_cache.TextColorOverrides != null &&
                _cache.TextColorOverrides.TryGetValue(key, out var stored) &&
                !string.IsNullOrWhiteSpace(stored))
            {
                return stored;
            }
            return defaultValue;
        }

        public async Task SetTextColorOverrideAsync(string key, string hex)
        {
            await _themeSettings.SetTextColorOverrideAsync(key, hex);
            SyncThemeFromService();
        }

        public async Task ClearAllTextColorOverridesAsync()
        {
            await _themeSettings.ClearAllTextColorOverridesAsync();
            SyncThemeFromService();
        }

        public bool IsTextColorCustomizationEnabled() => _cache.IsTextColorCustomizationEnabled;

        public async Task SetTextColorCustomizationEnabledAsync(bool enabled)
        {
            await _themeSettings.SetTextColorCustomizationEnabledAsync(enabled);
            SyncThemeFromService();
        }

        // Image selection highlight settings
        public string GetImageSelectionColor() => _cache.ImageSelectionColor;
        public async Task SetImageSelectionColorAsync(string color)
        {
            await _themeSettings.SetImageSelectionColorAsync(color);
            SyncThemeFromService();
        }

        public double GetImageSelectionOpacity() => _cache.ImageSelectionOpacity;
        public async Task SetImageSelectionOpacityAsync(double opacity)
        {
            await _themeSettings.SetImageSelectionOpacityAsync(opacity);
            SyncThemeFromService();
        }

        public double GetImageSelectionBorderThickness() => _cache.ImageSelectionBorderThickness;
        public async Task SetImageSelectionBorderThicknessAsync(double thickness)
        {
            await _themeSettings.SetImageSelectionBorderThicknessAsync(thickness);
            SyncThemeFromService();
        }

        public string GetImageDragSelectionColor() => _cache.ImageDragSelectionColor;
        public async Task SetImageDragSelectionColorAsync(string color)
        {
            await _themeSettings.SetImageDragSelectionColorAsync(color);
            SyncThemeFromService();
        }

        public double GetImageDragSelectionOpacity() => _cache.ImageDragSelectionOpacity;
        public async Task SetImageDragSelectionOpacityAsync(double opacity)
        {
            await _themeSettings.SetImageDragSelectionOpacityAsync(opacity);
            SyncThemeFromService();
        }

        // Viewport settings
        public CameraGesturePreset GetViewportCameraGesture() => _cache.ViewportCameraGesture;
        
        public async Task SetViewportCameraGestureAsync(CameraGesturePreset preset)
        {
            _cache.ViewportCameraGesture = preset;
            try
            {
                await _settingsStore.UpsertAsync("Viewport.CameraGesture", preset.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist Viewport.CameraGesture.");
            }
        }

        // Viewport Info Display - Model Stats
        public bool GetShowPolygonCount() => GetBoolSetting("Viewport.ShowPolygonCount", true);
        public async Task SetShowPolygonCountAsync(bool value) => await SetBoolSettingAsync("Viewport.ShowPolygonCount", value);
        
        public bool GetShowVertexCount() => GetBoolSetting("Viewport.ShowVertexCount", true);
        public async Task SetShowVertexCountAsync(bool value) => await SetBoolSettingAsync("Viewport.ShowVertexCount", value);
        
        public bool GetShowUVSetCount() => GetBoolSetting("Viewport.ShowUVSetCount", false);
        public async Task SetShowUVSetCountAsync(bool value) => await SetBoolSettingAsync("Viewport.ShowUVSetCount", value);
        
        public bool GetShowMaterialCount() => GetBoolSetting("Viewport.ShowMaterialCount", false);
        public async Task SetShowMaterialCountAsync(bool value) => await SetBoolSettingAsync("Viewport.ShowMaterialCount", value);
        
        public bool GetShowBoundingBox() => GetBoolSetting("Viewport.ShowBoundingBox", false);
        public async Task SetShowBoundingBoxAsync(bool value) => await SetBoolSettingAsync("Viewport.ShowBoundingBox", value);

        // Viewport Info Display - Viewport Stats
        public bool GetShowFPS() => GetBoolSetting("Viewport.ShowFPS", true);
        public async Task SetShowFPSAsync(bool value) => await SetBoolSettingAsync("Viewport.ShowFPS", value);
        
        public bool GetShowResolution() => GetBoolSetting("Viewport.ShowResolution", false);
        public async Task SetShowResolutionAsync(bool value) => await SetBoolSettingAsync("Viewport.ShowResolution", value);
        
        public bool GetShowViewportSize() => GetBoolSetting("Viewport.ShowViewportSize", false);
        public async Task SetShowViewportSizeAsync(bool value) => await SetBoolSettingAsync("Viewport.ShowViewportSize", value);
        
        public bool GetShowCameraInfo() => GetBoolSetting("Viewport.ShowCameraInfo", false);
        public async Task SetShowCameraInfoAsync(bool value) => await SetBoolSettingAsync("Viewport.ShowCameraInfo", value);

        // Rendering settings
        public bool GetBackfaceCulling() => GetBoolSetting("Viewport.BackfaceCulling", true);
        public async Task SetBackfaceCullingAsync(bool value) => await SetBoolSettingAsync("Viewport.BackfaceCulling", value);

        // Viewport background settings
        public ViewportBackgroundMode GetViewportBackgroundMode()
        {
            try
            {
                var value = _settingsStore.GetAsync("Viewport.BackgroundMode").GetAwaiter().GetResult();
                if (Enum.TryParse<ViewportBackgroundMode>(value, out var mode))
                    return mode;
            }
            catch { }
            return _cache.ViewportBackgroundMode;
        }

        public async Task SetViewportBackgroundModeAsync(ViewportBackgroundMode mode)
        {
            _cache.ViewportBackgroundMode = mode;
            try
            {
                await _settingsStore.UpsertAsync("Viewport.BackgroundMode", mode.ToString());
                _messenger?.Send(new Messages.SettingsChangedMessage("Viewport.BackgroundMode"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist Viewport.BackgroundMode.");
            }
        }

        public string GetViewportBackgroundColor() => _cache.ViewportBackgroundColor ?? "#3399CC";

        public async Task SetViewportBackgroundColorAsync(string hexColor)
        {
            _cache.ViewportBackgroundColor = hexColor ?? "#3399CC";
            try
            {
                await _settingsStore.UpsertAsync("Viewport.BackgroundColor", _cache.ViewportBackgroundColor);
                _messenger?.Send(new Messages.SettingsChangedMessage("Viewport.BackgroundColor"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist Viewport.BackgroundColor.");
            }
        }

        // Helper methods for bool settings
        private bool GetBoolSetting(string key, bool defaultValue)
        {
            try
            {
                var value = _settingsStore.GetAsync(key).GetAwaiter().GetResult();
                if (string.IsNullOrEmpty(value)) return defaultValue;
                return bool.TryParse(value, out bool result) ? result : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private async Task SetBoolSettingAsync(string key, bool value)
        {
            try
            {
                await _settingsStore.UpsertAsync(key, value.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"SettingsService: Failed to persist {key}.");
            }
        }

        // Material settings
        public (float R, float G, float B, float Metallic, float Roughness) GetMaterialParams()
        {
            return (_cache.MaterialAlbedoR, _cache.MaterialAlbedoG, _cache.MaterialAlbedoB, 
                    _cache.MaterialMetallic, _cache.MaterialRoughness);
        }

        public async Task SetMaterialParamsAsync(float r, float g, float b, float metallic, float roughness)
        {
            _cache.MaterialAlbedoR = r;
            _cache.MaterialAlbedoG = g;
            _cache.MaterialAlbedoB = b;
            _cache.MaterialMetallic = metallic;
            _cache.MaterialRoughness = roughness;
            try
            {
                await _settingsStore.UpsertAsync("Material.AlbedoR", r.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await _settingsStore.UpsertAsync("Material.AlbedoG", g.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await _settingsStore.UpsertAsync("Material.AlbedoB", b.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await _settingsStore.UpsertAsync("Material.Metallic", metallic.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await _settingsStore.UpsertAsync("Material.Roughness", roughness.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist Material params.");
            }
        }

        public List<MaterialPreset> GetMaterialPresets()
        {
            var defaultPresets = MaterialPreset.GetDefaultPresets();
            defaultPresets.AddRange(_cache.CustomMaterialPresets ?? new List<MaterialPreset>());
            return defaultPresets;
        }

        public async Task SetMaterialPresetsAsync(List<MaterialPreset> customPresets)
        {
            _cache.CustomMaterialPresets = customPresets ?? new List<MaterialPreset>();
            try
            {
                var json = JsonSerializer.Serialize(_cache.CustomMaterialPresets);
                await _settingsStore.UpsertAsync("Material.CustomPresets", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist Material presets.");
            }
        }

        // Key binding settings for Asset tab shortcuts
        public string GetAssetLitShortcut() => _cache.AssetLitShortcut ?? "Alt+1";
        
        public async Task SetAssetLitShortcutAsync(string shortcut)
        {
            _cache.AssetLitShortcut = shortcut ?? "Alt+1";
            try
            {
                await _settingsStore.UpsertAsync("KeyConfig.AssetLit", _cache.AssetLitShortcut);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist KeyConfig.AssetLit.");
            }
        }

        public string GetAssetDepthShortcut() => _cache.AssetDepthShortcut ?? "Alt+2";
        
        public async Task SetAssetDepthShortcutAsync(string shortcut)
        {
            _cache.AssetDepthShortcut = shortcut ?? "Alt+2";
            try
            {
                await _settingsStore.UpsertAsync("KeyConfig.AssetDepth", _cache.AssetDepthShortcut);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist KeyConfig.AssetDepth.");
            }
        }

        public string GetAssetWorldNormalShortcut() => _cache.AssetWorldNormalShortcut ?? "Alt+3";
        
        public async Task SetAssetWorldNormalShortcutAsync(string shortcut)
        {
            _cache.AssetWorldNormalShortcut = shortcut ?? "Alt+3";
            try
            {
                await _settingsStore.UpsertAsync("KeyConfig.AssetWorldNormal", _cache.AssetWorldNormalShortcut);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to persist KeyConfig.AssetWorldNormal.");
            }
        }

        // Lighting State Persistence
        public async Task<LightingPreset?> GetSavedLightingStateAsync()
        {
            try
            {
                var json = await _settingsStore.GetAsync("Viewport.LightingState");
                if (!string.IsNullOrWhiteSpace(json) && LooksLikeJsonObject(json))
                {
                    return JsonSerializer.Deserialize<LightingPreset>(json);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load lighting state.");
            }
            return null;
        }

        public async Task SaveLightingStateAsync(LightingPreset state)
        {
            try
            {
                var json = JsonSerializer.Serialize(state);
                await _settingsStore.UpsertAsync("Viewport.LightingState", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to save lighting state.");
            }
        }

        // Lighting Presets
        public async Task<List<LightingPreset>> GetLightingPresetsAsync()
        {
            try
            {
                var json = await _settingsStore.GetAsync("Viewport.LightingPresets");
                if (!string.IsNullOrWhiteSpace(json) && LooksLikeJsonArray(json))
                {
                    var presets = JsonSerializer.Deserialize<List<LightingPreset>>(json);
                    if (presets != null && presets.Count > 0)
                    {
                        return presets;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load lighting presets.");
            }
            // Return default presets if none saved
            return LightingPreset.GetDefaultPresets();
        }

        public async Task SaveLightingPresetsAsync(List<LightingPreset> presets)
        {
            try
            {
                var json = JsonSerializer.Serialize(presets);
                await _settingsStore.UpsertAsync("Viewport.LightingPresets", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to save lighting presets.");
            }
        }

        // Material State Persistence
        public async Task<MaterialPreset?> GetSavedMaterialStateAsync()
        {
            try
            {
                var json = await _settingsStore.GetAsync("Viewport.MaterialState");
                if (!string.IsNullOrWhiteSpace(json) && LooksLikeJsonObject(json))
                {
                    return JsonSerializer.Deserialize<MaterialPreset>(json);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load material state.");
            }
            return null;
        }

        public async Task SaveMaterialStateAsync(MaterialPreset state)
        {
            try
            {
                var json = JsonSerializer.Serialize(state);
                await _settingsStore.UpsertAsync("Viewport.MaterialState", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to save material state.");
            }
        }

    }
}
