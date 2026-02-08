using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Pivot.Messages;
using Pivot.Engine.Models;

namespace Pivot.Services
{
    /// <summary>
    /// Viewport関連設定の管理サービス。
    /// カメラ操作、表示オプション、背景設定を管理。
    /// </summary>
    public class ViewportSettingsService
    {
        private readonly ILogger<ViewportSettingsService> _logger;
        private readonly ISettingsStore _settingsStore;
        private readonly IMessenger? _messenger;

        // In-memory cache
        private CameraGesturePreset _cameraGesture = CameraGesturePreset.Maya;
        private ViewportBackgroundMode _backgroundMode = ViewportBackgroundMode.Custom;
        private string _backgroundColor = "#3399CC";
        private string _wireframeColor = "#FFFFFF";

        // Bool settings cache (loaded once at startup to avoid sync-over-async)
        private readonly Dictionary<string, bool> _boolCache = new();
        private static readonly Dictionary<string, bool> _boolDefaults = new()
        {
            ["Viewport.ShowPolygonCount"] = true,
            ["Viewport.ShowVertexCount"] = true,
            ["Viewport.ShowUVSetCount"] = false,
            ["Viewport.ShowMaterialCount"] = false,
            ["Viewport.ShowBoundingBox"] = false,
            ["Viewport.ShowFPS"] = true,
            ["Viewport.ShowResolution"] = false,
            ["Viewport.ShowViewportSize"] = false,
            ["Viewport.ShowCameraInfo"] = false,
            ["Viewport.ShowAxisGizmo"] = true,
            ["Viewport.BackfaceCulling"] = true,
            ["Viewport.ShowGrid"] = true,
        };

        private string _assetLitShortcut = "Alt+1";
        private string _assetDepthShortcut = "Alt+2";
        private string _assetWorldNormalShortcut = "Alt+3";

        public ViewportSettingsService(
            ILogger<ViewportSettingsService> logger,
            ISettingsStore settingsStore,
            IMessenger? messenger = null)
        {
            _logger = logger;
            _settingsStore = settingsStore;
            _messenger = messenger;
        }

        public async Task LoadAsync()
        {
            _logger.LogInformation("ViewportSettingsService: Loading...");
            try
            {
                // Camera gesture
                var gestureStr = await _settingsStore.GetAsync("Viewport.CameraGesture");
                if (Enum.TryParse<CameraGesturePreset>(gestureStr, out var gesture))
                    _cameraGesture = gesture;

                // Background mode
                var bgModeStr = await _settingsStore.GetAsync("Viewport.BackgroundMode");
                if (Enum.TryParse<ViewportBackgroundMode>(bgModeStr, out var bgMode))
                    _backgroundMode = bgMode;

                // Background color
                var bgColor = await _settingsStore.GetAsync("Viewport.BackgroundColor");
                if (!string.IsNullOrEmpty(bgColor))
                    _backgroundColor = bgColor;

                // Wireframe color
                var wfColor = await _settingsStore.GetAsync("Viewport.WireframeColor");
                if (!string.IsNullOrEmpty(wfColor))
                    _wireframeColor = wfColor;

                // Shortcuts
                var lit = await _settingsStore.GetAsync("KeyConfig.AssetLit");
                if (!string.IsNullOrEmpty(lit)) _assetLitShortcut = lit;

                var depth = await _settingsStore.GetAsync("KeyConfig.AssetDepth");
                if (!string.IsNullOrEmpty(depth)) _assetDepthShortcut = depth;

                var normal = await _settingsStore.GetAsync("KeyConfig.AssetWorldNormal");
                if (!string.IsNullOrEmpty(normal)) _assetWorldNormalShortcut = normal;

                // Preload all bool settings into cache
                foreach (var kvp in _boolDefaults)
                {
                    var val = await _settingsStore.GetAsync(kvp.Key);
                    _boolCache[kvp.Key] = bool.TryParse(val, out var parsed) ? parsed : kvp.Value;
                }

                _logger.LogInformation("ViewportSettingsService: Loaded");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ViewportSettingsService: Failed to load, using defaults.");
            }
        }

        // =============== Camera Gesture ===============

        public CameraGesturePreset GetCameraGesture() => _cameraGesture;

        public async Task SetCameraGestureAsync(CameraGesturePreset preset)
        {
            _cameraGesture = preset;
            try
            {
                await _settingsStore.UpsertAsync("Viewport.CameraGesture", preset.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ViewportSettingsService: Failed to persist CameraGesture.");
            }
        }

        // =============== Model Stats Display ===============

        public bool GetShowPolygonCount() => GetBoolSetting("Viewport.ShowPolygonCount", true);
        public Task SetShowPolygonCountAsync(bool value) => SetBoolSettingAsync("Viewport.ShowPolygonCount", value);

        public bool GetShowVertexCount() => GetBoolSetting("Viewport.ShowVertexCount", true);
        public Task SetShowVertexCountAsync(bool value) => SetBoolSettingAsync("Viewport.ShowVertexCount", value);

        public bool GetShowUVSetCount() => GetBoolSetting("Viewport.ShowUVSetCount", false);
        public Task SetShowUVSetCountAsync(bool value) => SetBoolSettingAsync("Viewport.ShowUVSetCount", value);

        public bool GetShowMaterialCount() => GetBoolSetting("Viewport.ShowMaterialCount", false);
        public Task SetShowMaterialCountAsync(bool value) => SetBoolSettingAsync("Viewport.ShowMaterialCount", value);

        public bool GetShowBoundingBox() => GetBoolSetting("Viewport.ShowBoundingBox", false);
        public Task SetShowBoundingBoxAsync(bool value) => SetBoolSettingAsync("Viewport.ShowBoundingBox", value);

        // =============== Viewport Stats Display ===============

        public bool GetShowFPS() => GetBoolSetting("Viewport.ShowFPS", true);
        public Task SetShowFPSAsync(bool value) => SetBoolSettingAsync("Viewport.ShowFPS", value);

        public bool GetShowResolution() => GetBoolSetting("Viewport.ShowResolution", false);
        public Task SetShowResolutionAsync(bool value) => SetBoolSettingAsync("Viewport.ShowResolution", value);

        public bool GetShowViewportSize() => GetBoolSetting("Viewport.ShowViewportSize", false);
        public Task SetShowViewportSizeAsync(bool value) => SetBoolSettingAsync("Viewport.ShowViewportSize", value);

        public bool GetShowCameraInfo() => GetBoolSetting("Viewport.ShowCameraInfo", false);
        public Task SetShowCameraInfoAsync(bool value) => SetBoolSettingAsync("Viewport.ShowCameraInfo", value);

        public bool GetShowAxisGizmo() => GetBoolSetting("Viewport.ShowAxisGizmo", true);
        public Task SetShowAxisGizmoAsync(bool value) => SetBoolSettingAsync("Viewport.ShowAxisGizmo", value);

        // =============== Rendering ===============

        public bool GetBackfaceCulling() => GetBoolSetting("Viewport.BackfaceCulling", true);
        public Task SetBackfaceCullingAsync(bool value) => SetBoolSettingAsync("Viewport.BackfaceCulling", value);

        public bool GetShowGrid() => GetBoolSetting("Viewport.ShowGrid", true);
        public Task SetShowGridAsync(bool value) => SetBoolSettingAsync("Viewport.ShowGrid", value);

        // =============== Shortcuts ===============

        public string GetAssetLitShortcut() => _assetLitShortcut;

        public async Task SetAssetLitShortcutAsync(string shortcut)
        {
            _assetLitShortcut = shortcut ?? "Alt+1";
            try
            {
                await _settingsStore.UpsertAsync("KeyConfig.AssetLit", _assetLitShortcut);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ViewportSettingsService: Failed to persist KeyConfig.AssetLit.");
            }
        }

        public string GetAssetDepthShortcut() => _assetDepthShortcut;

        public async Task SetAssetDepthShortcutAsync(string shortcut)
        {
            _assetDepthShortcut = shortcut ?? "Alt+2";
            try
            {
                await _settingsStore.UpsertAsync("KeyConfig.AssetDepth", _assetDepthShortcut);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ViewportSettingsService: Failed to persist KeyConfig.AssetDepth.");
            }
        }

        public string GetAssetWorldNormalShortcut() => _assetWorldNormalShortcut;

        public async Task SetAssetWorldNormalShortcutAsync(string shortcut)
        {
            _assetWorldNormalShortcut = shortcut ?? "Alt+3";
            try
            {
                await _settingsStore.UpsertAsync("KeyConfig.AssetWorldNormal", _assetWorldNormalShortcut);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ViewportSettingsService: Failed to persist KeyConfig.AssetWorldNormal.");
            }
        }

        // =============== Background ===============

        public ViewportBackgroundMode GetBackgroundMode() => _backgroundMode;

        public async Task SetBackgroundModeAsync(ViewportBackgroundMode mode)
        {
            _backgroundMode = mode;
            try
            {
                await _settingsStore.UpsertAsync("Viewport.BackgroundMode", mode.ToString());
                _messenger?.Send(new SettingsChangedMessage("Viewport.BackgroundMode"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ViewportSettingsService: Failed to persist BackgroundMode.");
            }
        }

        public string GetBackgroundColor() => _backgroundColor;

        public async Task SetBackgroundColorAsync(string hexColor)
        {
            _backgroundColor = hexColor ?? "#3399CC";
            try
            {
                await _settingsStore.UpsertAsync("Viewport.BackgroundColor", _backgroundColor);
                _messenger?.Send(new SettingsChangedMessage("Viewport.BackgroundColor"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ViewportSettingsService: Failed to persist BackgroundColor.");
            }
        }

        public string GetWireframeColor() => _wireframeColor;

        public async Task SetWireframeColorAsync(string hexColor)
        {
            _wireframeColor = hexColor ?? "#FFFFFF";
            try
            {
                await _settingsStore.UpsertAsync("Viewport.WireframeColor", _wireframeColor);
                _messenger?.Send(new SettingsChangedMessage("Viewport.WireframeColor"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ViewportSettingsService: Failed to persist WireframeColor.");
            }
        }

        // =============== Helper Methods ===============

        private bool GetBoolSetting(string key, bool defaultValue)
        {
            // Read from pre-loaded cache (no blocking async call)
            return _boolCache.TryGetValue(key, out var cached) ? cached : defaultValue;
        }

        private async Task SetBoolSettingAsync(string key, bool value)
        {
            try
            {
                await _settingsStore.UpsertAsync(key, value.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ViewportSettingsService: Failed to persist {Key}.", key);
            }
        }
    }
}
