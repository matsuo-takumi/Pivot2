using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.Engine.Models;
using Pivot.Models;
using Pivot.Services;
using System;
using Windows.UI;

namespace Pivot.Views
{
    public sealed partial class ViewportSettingsPage : Page
    {
        private readonly ViewportSettingsService? _settings;
        private bool _isInitializing = true;

        public ViewportSettingsPage()
        {
            this.InitializeComponent();
            _settings = App.Current.Services.GetService<ViewportSettingsService>();
            
            LoadCurrentSettings();
            _isInitializing = false;
        }

        private void LoadCurrentSettings()
        {
            try
            {
                // Camera gesture preset
                var preset = _settings?.GetCameraGesture() ?? CameraGesturePreset.Maya;
                
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
                
                // Background mode
                var bgMode = _settings?.GetBackgroundMode() ?? ViewportBackgroundMode.Custom;
                if (bgMode == ViewportBackgroundMode.Custom)
                {
                    CustomModeRadio.IsChecked = true;
                }
                else
                {
                    MatchThemeModeRadio.IsChecked = true;
                }
                UpdateColorPickerVisibility(bgMode);
                
                // Background color
                var bgColorHex = _settings?.GetBackgroundColor() ?? "#3399CC";
                BgColorPicker.Color = HexToColor(bgColorHex);
                
                // Show Grid

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ViewportSettingsPage.LoadCurrentSettings error: {ex.Message}");
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
                        await _settings.SetCameraGestureAsync(preset);
                        
                        UpdateGestureDisplay(preset);
                        
                        System.Diagnostics.Debug.WriteLine($"[ViewportSettingsPage] Saved gesture preset: {preset}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ViewportSettingsPage.GesturePresetComboBox_SelectionChanged error: {ex.Message}");
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

        private async void BackgroundModeRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                ViewportBackgroundMode mode = ViewportBackgroundMode.Custom;
                
                if (MatchThemeModeRadio.IsChecked == true)
                {
                    mode = ViewportBackgroundMode.MatchTheme;
                }
                
                if (_settings != null)
                {
                    await _settings.SetBackgroundModeAsync(mode);
                    System.Diagnostics.Debug.WriteLine($"[ViewportSettingsPage] Saved background mode: {mode}");
                }
                
                UpdateColorPickerVisibility(mode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ViewportSettingsPage.BackgroundModeRadio_SelectionChanged error: {ex.Message}");
            }
        }

        private void UpdateColorPickerVisibility(ViewportBackgroundMode mode)
        {
            ColorPickerPanel.Visibility = mode == ViewportBackgroundMode.Custom 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }

        private async void BgColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (_isInitializing) return;
            
            try
            {
                var color = args.NewColor;
                var hexColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                
                if (_settings != null)
                {
                    await _settings.SetBackgroundColorAsync(hexColor);
                    System.Diagnostics.Debug.WriteLine($"[ViewportSettingsPage] Saved background color: {hexColor}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ViewportSettingsPage.BgColorPicker_ColorChanged error: {ex.Message}");
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


    }
}

