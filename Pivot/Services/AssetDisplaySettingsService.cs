using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pivot.Models;

namespace Pivot.Services
{
    /// <summary>
    /// アセット表示設定の管理サービス。
    /// 表示モード、メタデータ表示、スキャン設定を管理。
    /// </summary>
    public class AssetDisplaySettingsService
    {
        private readonly ILogger<AssetDisplaySettingsService> _logger;
        private readonly ISettingsStore _settingsStore;

        // In-memory cache
        private string _displayMode = "Grid";
        private bool _showMetadata = true;
        private string _menuDisplayMode = "NameOnly";
        private bool _forceFullScan = false;

        public AssetDisplaySettingsService(
            ILogger<AssetDisplaySettingsService> logger,
            ISettingsStore settingsStore)
        {
            _logger = logger;
            _settingsStore = settingsStore;
        }

        public async Task LoadAsync()
        {
            _logger.LogInformation("AssetDisplaySettingsService: Loading...");
            try
            {
                // Display mode
                var displayMode = await _settingsStore.GetAsync("Asset.DisplayMode");
                if (!string.IsNullOrEmpty(displayMode))
                    _displayMode = displayMode;

                // Show metadata
                var showMeta = await _settingsStore.GetAsync("Asset.ShowMetadata");
                if (bool.TryParse(showMeta, out var meta))
                    _showMetadata = meta;

                // Menu display mode
                var menuMode = await _settingsStore.GetAsync("Asset.MenuDisplayMode");
                if (!string.IsNullOrEmpty(menuMode))
                    _menuDisplayMode = menuMode;

                // Force full scan
                var forceScan = await _settingsStore.GetAsync("Asset.ForceFullScan");
                if (bool.TryParse(forceScan, out var scan))
                    _forceFullScan = scan;

                _logger.LogInformation("AssetDisplaySettingsService: Loaded");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AssetDisplaySettingsService: Failed to load, using defaults.");
            }
        }

        // =============== Display Mode ===============

        public string GetDisplayMode() => _displayMode;

        public async Task SetDisplayModeAsync(string mode)
        {
            _displayMode = mode ?? "Grid";
            try
            {
                await _settingsStore.UpsertAsync("Asset.DisplayMode", _displayMode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AssetDisplaySettingsService: Failed to persist DisplayMode.");
            }
        }

        // =============== Show Metadata ===============

        public bool GetShowMetadata() => _showMetadata;

        public async Task SetShowMetadataAsync(bool show)
        {
            _showMetadata = show;
            try
            {
                await _settingsStore.UpsertAsync("Asset.ShowMetadata", show.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AssetDisplaySettingsService: Failed to persist ShowMetadata.");
            }
        }

        // =============== Menu Display Mode ===============

        public string GetMenuDisplayMode() => _menuDisplayMode;

        public async Task SetMenuDisplayModeAsync(string mode)
        {
            _menuDisplayMode = mode ?? "NameOnly";
            try
            {
                await _settingsStore.UpsertAsync("Asset.MenuDisplayMode", _menuDisplayMode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AssetDisplaySettingsService: Failed to persist MenuDisplayMode.");
            }
        }

        // =============== Force Full Scan ===============

        public bool GetForceFullScan() => _forceFullScan;

        public async Task SetForceFullScanAsync(bool force)
        {
            _forceFullScan = force;
            try
            {
                await _settingsStore.UpsertAsync("Asset.ForceFullScan", force.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AssetDisplaySettingsService: Failed to persist ForceFullScan.");
            }
        }
    }
}
