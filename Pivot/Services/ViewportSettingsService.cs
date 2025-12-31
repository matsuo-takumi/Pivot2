using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Pivot.Messages;
using Pivot.Models;

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

        // =============== Rendering ===============

        public bool GetBackfaceCulling() => GetBoolSetting("Viewport.BackfaceCulling", true);
        public Task SetBackfaceCullingAsync(bool value) => SetBoolSettingAsync("Viewport.BackfaceCulling", value);

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

        // =============== Helper Methods ===============

        private bool GetBoolSetting(string key, bool defaultValue)
        {
            try
            {
                var value = _settingsStore.GetAsync(key).GetAwaiter().GetResult();
                if (bool.TryParse(value, out var result))
                    return result;
            }
            catch { }
            return defaultValue;
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
