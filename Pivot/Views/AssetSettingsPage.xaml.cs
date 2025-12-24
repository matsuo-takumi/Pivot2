using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.Services;
using Pivot.ViewModels;
using Pivot.Models;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Logging;
using Windows.UI;
using System.Collections.ObjectModel;
using System.Linq;

namespace Pivot.Views
{
    public sealed partial class AssetSettingsPage : Page
    {
        private SettingsService? _settings;
        private bool _isInitializing = true;
        private List<MaterialPreset> _materialPresets = new();
        private List<LightingPreset> _lightingPresets = new();

        public AssetSettingsPage()
        {
            this.InitializeComponent();
            this.Loaded += AssetSettingsPage_Loaded;
        }

        private async void AssetSettingsPage_Loaded(object sender, RoutedEventArgs e)
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
                
                // Load background settings
                LoadBackgroundSettings();
                
                // Load display settings
                LoadDisplaySettings();
                
                // Load presets
                await LoadPresetsAsync();
            }
            
            _isInitializing = false;
        }

        private void LoadDisplaySettings()
        {
            if (_settings == null) return;
            
            ShowAxisGizmoToggle.IsOn = _settings.GetUserSettings().ViewportShowAxisGizmo;
            BackfaceCullingToggle.IsOn = _settings.GetBackfaceCulling();
            
            // Model Info toggles
            ShowPolygonCountToggle.IsOn = _settings.GetShowPolygonCount();
            ShowVertexCountToggle.IsOn = _settings.GetShowVertexCount();
            ShowUVSetCountToggle.IsOn = _settings.GetShowUVSetCount();
            ShowMaterialCountToggle.IsOn = _settings.GetShowMaterialCount();
            ShowBoundingBoxToggle.IsOn = _settings.GetShowBoundingBox();
            
            // Viewport Stats toggles
            ShowFPSToggle.IsOn = _settings.GetShowFPS();
            ShowResolutionToggle.IsOn = _settings.GetShowResolution();
            ShowViewportSizeToggle.IsOn = _settings.GetShowViewportSize();
            ShowCameraInfoToggle.IsOn = _settings.GetShowCameraInfo();
        }

        private async System.Threading.Tasks.Task LoadPresetsAsync()
        {
            if (_settings == null) return;
            
            // Load material presets
            _materialPresets = await _settings.GetMaterialPresetsAsync();
            MaterialPresetsList.ItemsSource = _materialPresets;
            
            // Load lighting presets
            _lightingPresets = await _settings.GetLightingPresetsAsync();
            LightingPresetsList.ItemsSource = _lightingPresets;
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
                    RotateGestureText.Text = "Alt + LMB";
                    PanGestureText.Text = "Alt + MMB";
                    ZoomGestureText.Text = "Alt + RMB";
                    break;
                    
                case CameraGesturePreset.Houdini:
                    RotateGestureText.Text = "LMB";
                    PanGestureText.Text = "MMB";
                    ZoomGestureText.Text = "RMB";
                    break;
                    
                case CameraGesturePreset.Blender:
                    RotateGestureText.Text = "MMB";
                    PanGestureText.Text = "Shift + MMB";
                    ZoomGestureText.Text = "Ctrl + MMB";
                    break;
            }
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

        // Material Preset handlers
        private void MaterialPresetsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Preview selected material preset (optional)
        }

        private async void AddMaterialPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
            
            var dialog = new ContentDialog
            {
                Title = "New Material Preset",
                Content = new TextBox { PlaceholderText = "Preset Name" },
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var textBox = dialog.Content as TextBox;
                var name = textBox?.Text?.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    // Create default preset with given name
                    var preset = new MaterialPreset
                    {
                        Name = name,
                        IsCustom = true,
                        Albedo = Windows.UI.Color.FromArgb(255, 200, 200, 200),
                        Metallic = 0.0f,
                        Roughness = 0.5f
                    };
                    _materialPresets.Add(preset);
                    await _settings.SaveMaterialPresetsAsync(_materialPresets);
                    MaterialPresetsList.ItemsSource = null;
                    MaterialPresetsList.ItemsSource = _materialPresets;
                }
            }
        }

        private async void DeleteMaterialPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null || sender is not Button button) return;
            
            if (button.DataContext is MaterialPreset preset)
            {
                _materialPresets.Remove(preset);
                await _settings.SaveMaterialPresetsAsync(_materialPresets);
                MaterialPresetsList.ItemsSource = null;
                MaterialPresetsList.ItemsSource = _materialPresets;
            }
        }

        // Lighting Preset handlers
        private void LightingPresetsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Preview selected lighting preset (optional)
        }

        private async void AddLightingPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
            
            var dialog = new ContentDialog
            {
                Title = "New Lighting Preset",
                Content = new TextBox { PlaceholderText = "Preset Name" },
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var textBox = dialog.Content as TextBox;
                var name = textBox?.Text?.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    // Create default preset with given name
                    var preset = LightingPreset.GetDefaultPresets().First().Clone();
                    preset.Name = name;
                    preset.IsCustom = true;
                    _lightingPresets.Add(preset);
                    await _settings.SaveLightingPresetsAsync(_lightingPresets);
                    LightingPresetsList.ItemsSource = null;
                    LightingPresetsList.ItemsSource = _lightingPresets;
                }
            }
        }

        private async void DeleteLightingPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null || sender is not Button button) return;
            
            if (button.DataContext is LightingPreset preset)
            {
                _lightingPresets.Remove(preset);
                await _settings.SaveLightingPresetsAsync(_lightingPresets);
                LightingPresetsList.ItemsSource = null;
                LightingPresetsList.ItemsSource = _lightingPresets;
            }
        }

        private static List<CustomFilter> GetDefaultAssetFilters()
        {
            // デフォルトタグなし - ユーザーが自分で作成
            return new List<CustomFilter>();
        }
    }
}

