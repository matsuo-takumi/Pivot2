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
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;

namespace Pivot.Views
{
    public sealed partial class AssetSettingsPage : Page
    {
        private ViewportSettingsService? _viewportSettings;
        private MaterialSettingsService? _materialSettings;
        private IMessenger? _messenger;
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
            // Initialize common filter settings view
            _viewportSettings = App.Current.Services.GetService<ViewportSettingsService>();
            _materialSettings = App.Current.Services.GetService<MaterialSettingsService>();
            _messenger = App.Current.Services.GetService<IMessenger>();
            
            if (_viewportSettings != null)
            {
                // Load viewport gesture preset
                LoadViewportGesturePreset();
                
                // Load background settings
                LoadBackgroundSettings();
                
                // Load display settings
                LoadDisplaySettings();
            }

            if (_materialSettings != null)
            {
                // Load presets
                await LoadPresetsAsync();
            }
            
            _isInitializing = false;
        }

        private void LoadDisplaySettings()
        {
            if (_viewportSettings == null) return;
            
            ShowAxisGizmoToggle.IsOn = _viewportSettings.GetShowAxisGizmo();
            BackfaceCullingToggle.IsOn = _viewportSettings.GetBackfaceCulling();
            
            // Model Info toggles
            ShowPolygonCountToggle.IsOn = _viewportSettings.GetShowPolygonCount();
            ShowVertexCountToggle.IsOn = _viewportSettings.GetShowVertexCount();
            ShowUVSetCountToggle.IsOn = _viewportSettings.GetShowUVSetCount();
            ShowMaterialCountToggle.IsOn = _viewportSettings.GetShowMaterialCount();
            ShowBoundingBoxToggle.IsOn = _viewportSettings.GetShowBoundingBox();
            
            // Viewport Stats toggles
            ShowFPSToggle.IsOn = _viewportSettings.GetShowFPS();
            ShowResolutionToggle.IsOn = _viewportSettings.GetShowResolution();
            ShowViewportSizeToggle.IsOn = _viewportSettings.GetShowViewportSize();
            ShowCameraInfoToggle.IsOn = _viewportSettings.GetShowCameraInfo();
            // Grid
            ShowGridToggle.IsOn = _viewportSettings.GetShowGrid();

            // Wireframe Color
            var wfColor = _viewportSettings.GetWireframeColor();
            WireframeColorPicker.Color = HexToColor(wfColor);
        }

        private async System.Threading.Tasks.Task LoadPresetsAsync()
        {
            if (_materialSettings == null) return;
            
            // Load material presets
            _materialPresets = new List<MaterialPreset>(await _materialSettings.GetMaterialPresetsAsync());
            MaterialPresetsList.ItemsSource = _materialPresets;
            
            // Load lighting presets
            _lightingPresets = await _materialSettings.GetLightingPresetsAsync();
            LightingPresetsList.ItemsSource = _lightingPresets;
        }

        private void LoadViewportGesturePreset()
        {
            try
            {
                var preset = _viewportSettings?.GetCameraGesture() ?? CameraGesturePreset.Maya;
                
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
                    if (_viewportSettings != null)
                    {
                        await _viewportSettings.SetCameraGestureAsync(preset);
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
            if (_viewportSettings == null) return;
            
            try
            {
                var mode = _viewportSettings.GetBackgroundMode();
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
                
                var hexColor = _viewportSettings.GetBackgroundColor();
                BgColorPicker.Color = HexToColor(hexColor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadBackgroundSettings error: {ex.Message}");
            }
        }

        private async void BackgroundModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _viewportSettings == null) return;
            
            try
            {
                if (BackgroundModeCombo.SelectedItem is ComboBoxItem item &&
                    item.Tag is string tagStr &&
                    Enum.TryParse<ViewportBackgroundMode>(tagStr, out var mode))
                {
                    await _viewportSettings.SetBackgroundModeAsync(mode);
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
            if (_isInitializing || _viewportSettings == null) return;
            
            try
            {
                var color = args.NewColor;
                var hexColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                await _viewportSettings.SetBackgroundColorAsync(hexColor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BgColorPicker_ColorChanged error: {ex.Message}");
            }

        }

        private async void WireframeColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (_isInitializing || _viewportSettings == null) return;

            try
            {
                var color = args.NewColor;
                var hexColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                await _viewportSettings.SetWireframeColorAsync(hexColor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WireframeColorPicker_ColorChanged error: {ex.Message}");
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

        private async void ShowAxisGizmoToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _viewportSettings == null) return;
            await _viewportSettings.SetShowAxisGizmoAsync(ShowAxisGizmoToggle.IsOn);
        }

        private async void BackfaceCullingToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _viewportSettings == null) return;
            await _viewportSettings.SetBackfaceCullingAsync(BackfaceCullingToggle.IsOn);
        }

        // Model Info toggles
        private async void ShowPolygonCountToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _viewportSettings == null) return;
            await _viewportSettings.SetShowPolygonCountAsync(ShowPolygonCountToggle.IsOn);
        }

        private async void ShowVertexCountToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _viewportSettings == null) return;
            await _viewportSettings.SetShowVertexCountAsync(ShowVertexCountToggle.IsOn);
        }

        private async void ShowUVSetCountToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _viewportSettings == null) return;
            await _viewportSettings.SetShowUVSetCountAsync(ShowUVSetCountToggle.IsOn);
        }

        private async void ShowMaterialCountToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _viewportSettings == null) return;
            await _viewportSettings.SetShowMaterialCountAsync(ShowMaterialCountToggle.IsOn);
        }

        private async void ShowBoundingBoxToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _viewportSettings == null) return;
            await _viewportSettings.SetShowBoundingBoxAsync(ShowBoundingBoxToggle.IsOn);
        }

        // Viewport Stats toggles
        private async void ShowFPSToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _viewportSettings == null) return;
            await _viewportSettings.SetShowFPSAsync(ShowFPSToggle.IsOn);
        }

        private async void ShowResolutionToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _viewportSettings == null) return;
            await _viewportSettings.SetShowResolutionAsync(ShowResolutionToggle.IsOn);
        }

        private async void ShowViewportSizeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _viewportSettings == null) return;
            await _viewportSettings.SetShowViewportSizeAsync(ShowViewportSizeToggle.IsOn);
        }

        private async void ShowCameraInfoToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _viewportSettings == null) return;
            await _viewportSettings.SetShowCameraInfoAsync(ShowCameraInfoToggle.IsOn);
        }

        private async void ShowGridToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _viewportSettings == null) return;
            await _viewportSettings.SetShowGridAsync(ShowGridToggle.IsOn);
            // Send message to notify viewport
            _messenger?.Send(new Pivot.Messages.SettingsChangedMessage("Viewport.ShowGrid"));
        }

        // Material Preset handlers
        private void MaterialPresetsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Preview selected material preset (optional)
        }

        private async void AddMaterialPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_materialSettings == null) return;
            
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
                    await _materialSettings.SetMaterialPresetsAsync(_materialPresets);
                    MaterialPresetsList.ItemsSource = null;
                    MaterialPresetsList.ItemsSource = _materialPresets;
                }
            }
        }

        private async void DeleteMaterialPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_materialSettings == null || sender is not Button button) return;
            
            if (button.DataContext is MaterialPreset preset)
            {
                _materialPresets.Remove(preset);
                await _materialSettings.SetMaterialPresetsAsync(_materialPresets);
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
            if (_materialSettings == null) return;
            
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
                    await _materialSettings.SaveLightingPresetsAsync(_lightingPresets);
                    LightingPresetsList.ItemsSource = null;
                    LightingPresetsList.ItemsSource = _lightingPresets;
                }
            }
        }

        private async void DeleteLightingPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_materialSettings == null || sender is not Button button) return;
            
            if (button.DataContext is LightingPreset preset)
            {
                _lightingPresets.Remove(preset);
                await _materialSettings.SaveLightingPresetsAsync(_lightingPresets);
                LightingPresetsList.ItemsSource = null;
                LightingPresetsList.ItemsSource = _lightingPresets;
            }
        }


    }
}

