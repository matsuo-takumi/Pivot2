using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.Services;
using Pivot.ViewModels;
using Pivot.Models;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Logging;
using Windows.UI;

namespace Pivot.Views
{
    public sealed partial class AssetSettingsPage : Page
    {
        private SettingsService? _settings;
        private bool _isInitializing = true;

        public AssetSettingsPage()
        {
            this.InitializeComponent();
            this.Loaded += AssetSettingsPage_Loaded;
        }

        private void AssetSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize common filter settings view
            _settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
            var logger = App.Current.Services.GetService(typeof(ILogger<FilterSettingsViewModel>)) as ILogger<FilterSettingsViewModel>;
            
            if (_settings != null)
            {
                var filterView = this.FindName("FilterSettingsView") as FilterSettingsView;
                if (filterView != null)
                {
                    filterView.ViewModel = new FilterSettingsViewModel(
                        _settings,
                        FilterType.Asset,
                        "Asset",
                        GetDefaultAssetFilters,
                        logger);
                }
                
                // Load viewport gesture preset
                LoadViewportGesturePreset();
                
                // Load grid settings
                LoadGridSettings();
                
                // Load background settings
                LoadBackgroundSettings();
            }
            
            _isInitializing = false;
        }

        private void LoadViewportGesturePreset()
        {
            try
            {
                var preset = _settings?.GetViewportCameraGesture() ?? CameraGesturePreset.Maya;
                
                // Select the correct ComboBox item
                for (int i = 0; i < GesturePresetComboBox.Items.Count; i++)
                {
                    if (GesturePresetComboBox.Items[i] is ComboBoxItem item && 
                        item.Tag?.ToString() == preset.ToString())
                    {
                        GesturePresetComboBox.SelectedIndex = i;
                        break;
                    }
                }
                
                UpdateGestureDisplay(preset);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadViewportGesturePreset error: {ex.Message}");
            }
        }

        private async void GesturePresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                if (GesturePresetComboBox.SelectedItem is ComboBoxItem item &&
                    item.Tag is string tagStr &&
                    Enum.TryParse<CameraGesturePreset>(tagStr, out var preset))
                {
                    if (_settings != null)
                    {
                        await _settings.SetViewportCameraGestureAsync(preset);
                        UpdateGestureDisplay(preset);
                        System.Diagnostics.Debug.WriteLine($"[AssetSettingsPage] Saved gesture preset: {preset}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GesturePresetComboBox_SelectionChanged error: {ex.Message}");
            }
        }

        private void UpdateGestureDisplay(CameraGesturePreset preset)
        {
            switch (preset)
            {
                case CameraGesturePreset.Maya:
                    RotateGestureText.Text = "Alt + Left Click";
                    PanGestureText.Text = "Alt + Middle Click";
                    ZoomGestureText.Text = "Alt + Right Click";
                    break;
                    
                case CameraGesturePreset.Houdini:
                    RotateGestureText.Text = "Left Click";
                    PanGestureText.Text = "Middle Click";
                    ZoomGestureText.Text = "Right Click";
                    break;
                    
                case CameraGesturePreset.Blender:
                    RotateGestureText.Text = "Middle Click";
                    PanGestureText.Text = "Shift + Middle Click";
                    ZoomGestureText.Text = "Ctrl + Middle Click";
                    break;
            }
        }

        private void LoadGridSettings()
        {
            if (_settings == null) return;
            
            var userSettings = _settings.GetUserSettings();
            ShowGridToggle.IsOn = userSettings.ViewportShowGrid;
            GridSizeBox.Value = userSettings.ViewportGridSize;
            GridSpacingBox.Value = userSettings.ViewportGridSpacing;
            ShowAxisGizmoToggle.IsOn = userSettings.ViewportShowAxisGizmo;
            BackfaceCullingToggle.IsOn = _settings.GetBackfaceCulling();
            
            // Load info display toggles
            ShowPolygonCountToggle.IsOn = _settings.GetShowPolygonCount();
            ShowVertexCountToggle.IsOn = _settings.GetShowVertexCount();
            ShowUVSetCountToggle.IsOn = _settings.GetShowUVSetCount();
            ShowMaterialCountToggle.IsOn = _settings.GetShowMaterialCount();
            ShowBoundingBoxToggle.IsOn = _settings.GetShowBoundingBox();
            
            ShowFPSToggle.IsOn = _settings.GetShowFPS();
            ShowResolutionToggle.IsOn = _settings.GetShowResolution();
            ShowViewportSizeToggle.IsOn = _settings.GetShowViewportSize();
            ShowCameraInfoToggle.IsOn = _settings.GetShowCameraInfo();
        }

        private void LoadBackgroundSettings()
        {
            if (_settings == null) return;
            
            try
            {
                var mode = _settings.GetViewportBackgroundMode();
                for (int i = 0; i < BackgroundModeCombo.Items.Count; i++)
                {
                    if (BackgroundModeCombo.Items[i] is ComboBoxItem item &&
                        item.Tag?.ToString() == mode.ToString())
                    {
                        BackgroundModeCombo.SelectedIndex = i;
                        break;
                    }
                }
                UpdateBackgroundColorPanelVisibility(mode);
                
                var hexColor = _settings.GetViewportBackgroundColor();
                BgColorPicker.Color = HexToColor(hexColor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadBackgroundSettings error: {ex.Message}");
            }
        }

        private async void BackgroundModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _settings == null) return;
            
            try
            {
                if (BackgroundModeCombo.SelectedItem is ComboBoxItem item &&
                    item.Tag is string tagStr &&
                    Enum.TryParse<ViewportBackgroundMode>(tagStr, out var mode))
                {
                    await _settings.SetViewportBackgroundModeAsync(mode);
                    UpdateBackgroundColorPanelVisibility(mode);
                    System.Diagnostics.Debug.WriteLine($"[AssetSettingsPage] Saved background mode: {mode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BackgroundModeCombo_SelectionChanged error: {ex.Message}");
            }
        }

        private void UpdateBackgroundColorPanelVisibility(ViewportBackgroundMode mode)
        {
            BackgroundColorPanel.Visibility = mode == ViewportBackgroundMode.Custom 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }

        private async void BgColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (_isInitializing || _settings == null) return;
            
            try
            {
                var color = args.NewColor;
                var hexColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                await _settings.SetViewportBackgroundColorAsync(hexColor);
                System.Diagnostics.Debug.WriteLine($"[AssetSettingsPage] Saved background color: {hexColor}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BgColorPicker_ColorChanged error: {ex.Message}");
            }
        }

        private static Color HexToColor(string hex)
        {
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length == 6)
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return Color.FromArgb(255, r, g, b);
                }
            }
            catch { }
            return Color.FromArgb(255, 51, 153, 204); // Default
        }

        private void ShowGridToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settings == null) return;
            var userSettings = _settings.GetUserSettings();
            userSettings.ViewportShowGrid = ShowGridToggle.IsOn;
            // Save not implemented yet - grid rendering is a future enhancement
        }

        private void GridSizeBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_isInitializing || _settings == null) return;
            var userSettings = _settings.GetUserSettings();
            userSettings.ViewportGridSize = (float)args.NewValue;
        }

        private void GridSpacingBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_isInitializing || _settings == null) return;
            var userSettings = _settings.GetUserSettings();
            userSettings.ViewportGridSpacing = (float)args.NewValue;
        }

        private void ShowAxisGizmoToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settings == null) return;
            var userSettings = _settings.GetUserSettings();
            userSettings.ViewportShowAxisGizmo = ShowAxisGizmoToggle.IsOn;
        }

        private async void BackfaceCullingToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settings == null) return;
            await _settings.SetBackfaceCullingAsync(BackfaceCullingToggle.IsOn);
        }

        // Model Info toggles
        private async void ShowPolygonCountToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settings == null) return;
            await _settings.SetShowPolygonCountAsync(ShowPolygonCountToggle.IsOn);
        }

        private async void ShowVertexCountToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settings == null) return;
            await _settings.SetShowVertexCountAsync(ShowVertexCountToggle.IsOn);
        }

        private async void ShowUVSetCountToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settings == null) return;
            await _settings.SetShowUVSetCountAsync(ShowUVSetCountToggle.IsOn);
        }

        private async void ShowMaterialCountToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settings == null) return;
            await _settings.SetShowMaterialCountAsync(ShowMaterialCountToggle.IsOn);
        }

        private async void ShowBoundingBoxToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settings == null) return;
            await _settings.SetShowBoundingBoxAsync(ShowBoundingBoxToggle.IsOn);
        }

        // Viewport Stats toggles
        private async void ShowFPSToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settings == null) return;
            await _settings.SetShowFPSAsync(ShowFPSToggle.IsOn);
        }

        private async void ShowResolutionToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settings == null) return;
            await _settings.SetShowResolutionAsync(ShowResolutionToggle.IsOn);
        }

        private async void ShowViewportSizeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settings == null) return;
            await _settings.SetShowViewportSizeAsync(ShowViewportSizeToggle.IsOn);
        }

        private async void ShowCameraInfoToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settings == null) return;
            await _settings.SetShowCameraInfoAsync(ShowCameraInfoToggle.IsOn);
        }

        private static List<CustomFilter> GetDefaultAssetFilters()
        {
            // デフォルトタグなし - ユーザーが自分で作成
            return new List<CustomFilter>();
        }
    }
}
