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
        private readonly FilterSettingsService _filterSettings;
        private readonly ViewportSettingsService _viewportSettings;
        private readonly MaterialSettingsService _materialSettings;
        private readonly ImageDisplaySettingsService _imageDisplaySettings;
        private readonly AssetDisplaySettingsService _assetDisplaySettings;
        private readonly CodeSettingsService _codeSettings;

        // In-memory cache to avoid sync-over-async and improve UI responsiveness
        private UserSettings _cache; // Settingsをメモリにキャッシュ

        public SettingsService(
            ILogger<SettingsService> logger,
            MetadataService metadataService,
            IMessenger messenger, 
            DirectorySettingsService directorySettings,
            ThemeSettingsService themeSettings,
            FilterSettingsService filterSettings,
            ViewportSettingsService viewportSettings,
            MaterialSettingsService materialSettings,
            ImageDisplaySettingsService imageDisplaySettings,
            AssetDisplaySettingsService assetDisplaySettings,
            CodeSettingsService codeSettings,
            ISettingsStore? settingsStore = null)
        {
            _logger = logger;
            _metadataService = metadataService;
            _messenger = messenger;
            _directorySettings = directorySettings;
            _themeSettings = themeSettings;
            _filterSettings = filterSettings;
            _viewportSettings = viewportSettings;
            _materialSettings = materialSettings;
            _imageDisplaySettings = imageDisplaySettings;
            _assetDisplaySettings = assetDisplaySettings;
            _codeSettings = codeSettings;
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
            await _filterSettings.LoadAsync();
            await _viewportSettings.LoadAsync();
            await _materialSettings.LoadAsync();
            await _imageDisplaySettings.LoadAsync();
            await _assetDisplaySettings.LoadAsync();
            await _codeSettings.LoadAsync();
            
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
            _logger.LogInformation("SettingsService: Loading settings from sub-services...");

            // All settings are now loaded by their respective sub-services in InitializeAsync.
            // This method syncs the legacy _cache from sub-services for backward compatibility.

            // Sync from DirectorySettingsService
            SyncDirectoriesFromService();

            // Sync from ThemeSettingsService
            SyncThemeFromService();

            // Sync from FilterSettingsService
            _cache.AssetFilters = _filterSettings.GetAssetFilters();
            _cache.CodeFilters = _filterSettings.GetCodeFilters();
            _cache.CodeCategories = _filterSettings.GetCodeCategories();
            // Note: VisibleFiltersByTab and SelectedFiltersByTab are managed by FilterSettingsService

            // Sync from ImageDisplaySettingsService
            _cache.ImageLayoutMode = _imageDisplaySettings.GetLayoutMode();
            _cache.ImageSortField = _imageDisplaySettings.GetSortField().ToString();
            _cache.ImageSortDirection = _imageDisplaySettings.GetSortDirection().ToString();

            // Sync from ViewportSettingsService
            _cache.ViewportCameraGesture = _viewportSettings.GetCameraGesture();

            // Sync from AssetDisplaySettingsService
            if (Enum.TryParse<AssetDisplayMode>(_assetDisplaySettings.GetDisplayMode(), out var mode))
                _cache.AssetDisplayMode = mode;
            else
                _cache.AssetDisplayMode = AssetDisplayMode.Grid;
            _cache.ShowAssetMetadata = _assetDisplaySettings.GetShowMetadata();
            _cache.ForceFullScan = _assetDisplaySettings.GetForceFullScan();
            if (Enum.TryParse<MenuDisplayMode>(_assetDisplaySettings.GetMenuDisplayMode(), out var menuMode))
                _cache.MenuDisplayMode = menuMode;
            else
                _cache.MenuDisplayMode = MenuDisplayMode.Compact;

            // Sync from CodeSettingsService
            _cache.CodeExportFormat = _codeSettings.GetCodeExportFormat();
            _cache.LastSelectedSnippetId = _codeSettings.GetLastSelectedSnippetId() ?? Guid.Empty;

            _logger.LogInformation("SettingsService: Settings synced from sub-services.");
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
            var fmt = _codeSettings.GetCodeExportFormat();
            if (Enum.TryParse<Pivot.Models.CodeExportFormat>(fmt, out var result)) return result;
            return Pivot.Models.CodeExportFormat.Json;
        }

        public async Task SetCodeExportFormatAsync(Pivot.Models.CodeExportFormat format)
        {
            await _codeSettings.SetCodeExportFormatAsync(format.ToString());
        }

        public Guid GetLastSelectedSnippetId()
        {
            return _codeSettings.GetLastSelectedSnippetId() ?? Guid.Empty;
        }

        public async Task SetLastSelectedSnippetIdAsync(Guid id)
        {
            await _codeSettings.SetLastSelectedSnippetIdAsync(id);
        }

        // Asset 表示モード/メタ表示 設定
        public AssetDisplayMode GetAssetDisplayMode()
        {
            var mode = _assetDisplaySettings.GetDisplayMode();
            if (Enum.TryParse<AssetDisplayMode>(mode, out var result)) return result;
            return AssetDisplayMode.Grid;
        }

        public async Task SetAssetDisplayModeAsync(AssetDisplayMode mode)
        {
            await _assetDisplaySettings.SetDisplayModeAsync(mode.ToString());
        }

        public bool GetShowAssetMetadata() => _assetDisplaySettings.GetShowMetadata();

        public async Task SetShowAssetMetadataAsync(bool show)
        {
            await _assetDisplaySettings.SetShowMetadataAsync(show);
        }

        // スキャン戦略設定
        public bool GetForceFullScan() => _assetDisplaySettings.GetForceFullScan();

        public async Task SetForceFullScanAsync(bool force)
        {
            await _assetDisplaySettings.SetForceFullScanAsync(force);
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

        // =============== Filter Settings (delegated to FilterSettingsService) ===============
        
        // AssetFilters accessors
        public List<Pivot.Models.CustomFilter> GetAssetFilters() => _filterSettings.GetAssetFilters();
        public Task SetAssetFiltersAsync(List<Pivot.Models.CustomFilter> filters) => _filterSettings.SetAssetFiltersAsync(filters);

        // CodeFilters accessors
        public List<Pivot.Models.CustomFilter> GetCodeFilters() => _filterSettings.GetCodeFilters();
        public Task SetCodeFiltersAsync(List<Pivot.Models.CustomFilter> filters) => _filterSettings.SetCodeFiltersAsync(filters);

        // CodeCategories accessors
        public List<Pivot.Models.CodeCategory> GetCodeCategories() => _filterSettings.GetCodeCategories();
        public Task SetCodeCategoriesAsync(List<Pivot.Models.CodeCategory> categories) => _filterSettings.SetCodeCategoriesAsync(categories);

        public string GetFilterNameById(Guid filterId) => _filterSettings.GetFilterNameById(filterId);
        public Task<string?> UpdateFilterNameAsync(Guid filterId, string newName) => _filterSettings.UpdateFilterNameAsync(filterId, newName);
        public Task<string?> DeleteFilterAsync(Guid filterId) => _filterSettings.DeleteFilterAsync(filterId);

        // Visible filters per tab accessors
        public List<Guid> GetVisibleFiltersForTab(string tabId) => _filterSettings.GetVisibleFiltersForTab(tabId);
        public Task SetVisibleFiltersForTabAsync(string tabId, List<Guid> filterIds) => _filterSettings.SetVisibleFiltersForTabAsync(tabId, filterIds);

        public List<Guid> GetSelectedFiltersForTab(string tabId) => _filterSettings.GetSelectedFiltersForTab(tabId);
        public Task SetSelectedFiltersForTabAsync(string tabId, List<Guid> filterIds) => _filterSettings.SetSelectedFiltersForTabAsync(tabId, filterIds);

        // Overlay tint accessors - delegated to ThemeSettingsService
        public string GetOverlayTintColor() => _themeSettings.OverlayTintColor ?? "#0000FF";

        public Task SetOverlayTintColorAsync(string color) => _themeSettings.SetOverlayTintColorAsync(color);

        public double GetOverlayTintOpacity() => _themeSettings.OverlayTintOpacity;

        public Task SetOverlayTintOpacityAsync(double value) => _themeSettings.SetOverlayTintOpacityAsync(value);

        public double GetOverlayTintLuminosityOpacity() => _themeSettings.OverlayTintLuminosityOpacity;

        public Task SetOverlayTintLuminosityOpacityAsync(double value) => _themeSettings.SetOverlayTintLuminosityOpacityAsync(value);

        public int GetOverlayTintTransitionDurationMs() => _themeSettings.OverlayTintTransitionDurationMs;

        public Task SetOverlayTintTransitionDurationMsAsync(int milliseconds) => _themeSettings.SetOverlayTintTransitionDurationMsAsync(milliseconds);



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

        // Image selection highlight settings - delegated to ThemeSettingsService
        public string GetImageSelectionColor() => _themeSettings.ImageSelectionColor;
        public Task SetImageSelectionColorAsync(string color) => _themeSettings.SetImageSelectionColorAsync(color);

        public double GetImageSelectionOpacity() => _themeSettings.ImageSelectionOpacity;
        public Task SetImageSelectionOpacityAsync(double opacity) => _themeSettings.SetImageSelectionOpacityAsync(opacity);

        public double GetImageSelectionBorderThickness() => _themeSettings.ImageSelectionBorderThickness;
        public Task SetImageSelectionBorderThicknessAsync(double thickness) => _themeSettings.SetImageSelectionBorderThicknessAsync(thickness);

        public string GetImageDragSelectionColor() => _themeSettings.ImageDragSelectionColor;
        public Task SetImageDragSelectionColorAsync(string color) => _themeSettings.SetImageDragSelectionColorAsync(color);

        public double GetImageDragSelectionOpacity() => _themeSettings.ImageDragSelectionOpacity;
        public Task SetImageDragSelectionOpacityAsync(double opacity) => _themeSettings.SetImageDragSelectionOpacityAsync(opacity);

        // Image layout and sort settings - delegated to ImageDisplaySettingsService
        public LayoutType GetImageLayoutMode() => _imageDisplaySettings.GetLayoutMode();
        public Task SetImageLayoutModeAsync(LayoutType layout) => _imageDisplaySettings.SetLayoutModeAsync(layout);

        public SortField GetImageSortField() => _imageDisplaySettings.GetSortField();
        public Task SetImageSortFieldAsync(SortField field) => _imageDisplaySettings.SetSortFieldAsync(field);

        public SortDirection GetImageSortDirection() => _imageDisplaySettings.GetSortDirection();
        public Task SetImageSortDirectionAsync(SortDirection direction) => _imageDisplaySettings.SetSortDirectionAsync(direction);

        // =============== Viewport Settings (delegated to ViewportSettingsService) ===============
        
        public CameraGesturePreset GetViewportCameraGesture() => _viewportSettings.GetCameraGesture();
        public Task SetViewportCameraGestureAsync(CameraGesturePreset preset) => _viewportSettings.SetCameraGestureAsync(preset);

        // Viewport Info Display - Model Stats
        public bool GetShowPolygonCount() => _viewportSettings.GetShowPolygonCount();
        public Task SetShowPolygonCountAsync(bool value) => _viewportSettings.SetShowPolygonCountAsync(value);
        public bool GetShowVertexCount() => _viewportSettings.GetShowVertexCount();
        public Task SetShowVertexCountAsync(bool value) => _viewportSettings.SetShowVertexCountAsync(value);
        public bool GetShowUVSetCount() => _viewportSettings.GetShowUVSetCount();
        public Task SetShowUVSetCountAsync(bool value) => _viewportSettings.SetShowUVSetCountAsync(value);
        public bool GetShowMaterialCount() => _viewportSettings.GetShowMaterialCount();
        public Task SetShowMaterialCountAsync(bool value) => _viewportSettings.SetShowMaterialCountAsync(value);
        public bool GetShowBoundingBox() => _viewportSettings.GetShowBoundingBox();
        public Task SetShowBoundingBoxAsync(bool value) => _viewportSettings.SetShowBoundingBoxAsync(value);

        // Viewport Info Display - Viewport Stats
        public bool GetShowFPS() => _viewportSettings.GetShowFPS();
        public Task SetShowFPSAsync(bool value) => _viewportSettings.SetShowFPSAsync(value);
        public bool GetShowResolution() => _viewportSettings.GetShowResolution();
        public Task SetShowResolutionAsync(bool value) => _viewportSettings.SetShowResolutionAsync(value);
        public bool GetShowViewportSize() => _viewportSettings.GetShowViewportSize();
        public Task SetShowViewportSizeAsync(bool value) => _viewportSettings.SetShowViewportSizeAsync(value);
        public bool GetShowCameraInfo() => _viewportSettings.GetShowCameraInfo();
        public Task SetShowCameraInfoAsync(bool value) => _viewportSettings.SetShowCameraInfoAsync(value);

        // Rendering settings
        public bool GetBackfaceCulling() => _viewportSettings.GetBackfaceCulling();
        public Task SetBackfaceCullingAsync(bool value) => _viewportSettings.SetBackfaceCullingAsync(value);

        // Viewport background settings
        public ViewportBackgroundMode GetViewportBackgroundMode() => _viewportSettings.GetBackgroundMode();
        public Task SetViewportBackgroundModeAsync(ViewportBackgroundMode mode) => _viewportSettings.SetBackgroundModeAsync(mode);
        public string GetViewportBackgroundColor() => _viewportSettings.GetBackgroundColor();
        public Task SetViewportBackgroundColorAsync(string hexColor) => _viewportSettings.SetBackgroundColorAsync(hexColor);

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

        // Material Presets
        public async Task<List<MaterialPreset>> GetMaterialPresetsAsync()
        {
            try
            {
                var json = await _settingsStore.GetAsync("Viewport.MaterialPresets");
                if (!string.IsNullOrWhiteSpace(json) && LooksLikeJsonArray(json))
                {
                    var presets = JsonSerializer.Deserialize<List<MaterialPreset>>(json);
                    if (presets != null && presets.Count > 0)
                    {
                        return presets;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to load material presets.");
            }
            // Return default presets if none saved
            return MaterialPreset.GetDefaultPresets();
        }

        public async Task SaveMaterialPresetsAsync(List<MaterialPreset> presets)
        {
            try
            {
                var json = JsonSerializer.Serialize(presets);
                await _settingsStore.UpsertAsync("Viewport.MaterialPresets", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsService: Failed to save material presets.");
            }
        }

    }
}
