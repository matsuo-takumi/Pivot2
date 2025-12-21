using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.Models;
using Pivot.Services;
using System;

namespace Pivot.Views
{
    public sealed partial class ViewportSettingsPage : Page
    {
        private readonly SettingsService? _settings;
        private bool _isInitializing = true;

        public ViewportSettingsPage()
        {
            this.InitializeComponent();
            _settings = App.Current.Services.GetService<SettingsService>();
            
            LoadCurrentSettings();
            _isInitializing = false;
        }

        private void LoadCurrentSettings()
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
                        await _settings.SetViewportCameraGestureAsync(preset);
                        
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
    }
}
