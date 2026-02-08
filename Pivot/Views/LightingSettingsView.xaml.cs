using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.Engine.Models;
using Pivot.Services;
using Pivot.ViewModels;
using System;
using System.Collections.Generic;
using Windows.UI;

namespace Pivot.Views
{
    public sealed partial class LightingSettingsView : UserControl
    {
        private bool _isUpdating = false;
        private List<LightingPreset> _presets = new();
        private MaterialSettingsService? _settingsService;

        public ModelViewerViewModel? ViewModel { get; set; }

        public LightingSettingsView()
        {
            this.InitializeComponent();
            this.Loaded += LightingSettingsView_Loaded;
            _settingsService = App.Current?.Services?.GetService<MaterialSettingsService>();
        }

        private async void LightingSettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            // Load presets
            if (_settingsService != null)
            {
                _presets = await _settingsService.GetLightingPresetsAsync();
                PresetComboBox.Items.Clear();
                foreach (var preset in _presets)
                {
                    PresetComboBox.Items.Add(preset.Name);
                }
            }

            if (ViewModel != null)
            {
                InitializeFromViewModel();
            }
        }

        private void InitializeFromViewModel()
        {
            if (ViewModel == null) return;

            _isUpdating = true;

            // Key Light
            KeyIntensitySlider.Value = ViewModel.LightIntensity;
            KeyIntensityValueText.Text = ViewModel.LightIntensity.ToString("F2");
            KeyColorPicker.Color = ViewModel.LightColor;
            YawSlider.Value = ViewModel.LightYaw;
            YawValueText.Text = ViewModel.LightYaw.ToString("F2");
            PitchSlider.Value = ViewModel.LightPitch;
            PitchValueText.Text = ViewModel.LightPitch.ToString("F2");

            // Ambient Light
            AmbientIntensitySlider.Value = ViewModel.AmbientIntensity;
            AmbientIntensityValueText.Text = ViewModel.AmbientIntensity.ToString("F2");
            AmbientColorPicker.Color = ViewModel.AmbientColor;

            // Rim Light
            RimIntensitySlider.Value = ViewModel.RimIntensity;
            RimIntensityValueText.Text = ViewModel.RimIntensity.ToString("F2");
            RimColorPicker.Color = ViewModel.RimColor;

            // Back Light
            BackIntensitySlider.Value = ViewModel.BackIntensity;
            BackIntensityValueText.Text = ViewModel.BackIntensity.ToString("F2");
            BackColorPicker.Color = ViewModel.BackColor;

            _isUpdating = false;
        }

        private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdating || ViewModel == null) return;
            if (PresetComboBox.SelectedIndex < 0 || PresetComboBox.SelectedIndex >= _presets.Count) return;

            var preset = _presets[PresetComboBox.SelectedIndex];
            ViewModel.ApplyLightingPreset(preset);
            InitializeFromViewModel();
        }

        private async void SavePresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null || _settingsService == null) return;

            var dialog = new ContentDialog
            {
                Title = "Save Preset",
                Content = new TextBox { PlaceholderText = "Preset Name" },
                PrimaryButtonText = "Save",
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
                    var preset = ViewModel.CreateLightingPresetFromCurrent(name);
                    _presets.Add(preset);
                    await _settingsService.SaveLightingPresetsAsync(_presets);
                    PresetComboBox.Items.Add(name);
                    PresetComboBox.SelectedIndex = _presets.Count - 1;
                }
            }
        }

        // Key Light handlers
        private void KeyIntensitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (KeyIntensityValueText != null) KeyIntensityValueText.Text = e.NewValue.ToString("F2");
            if (ViewModel != null) ViewModel.LightIntensity = (float)e.NewValue;
        }

        private void KeyColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (_isUpdating) return;
            if (ViewModel != null) ViewModel.LightColor = args.NewColor;
        }

        private void YawSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (YawValueText != null) YawValueText.Text = e.NewValue.ToString("F2");
            if (ViewModel != null) ViewModel.LightYaw = (float)e.NewValue;
        }

        private void PitchSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (PitchValueText != null) PitchValueText.Text = e.NewValue.ToString("F2");
            if (ViewModel != null) ViewModel.LightPitch = (float)e.NewValue;
        }

        // Ambient Light handlers
        private void AmbientIntensitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (AmbientIntensityValueText != null) AmbientIntensityValueText.Text = e.NewValue.ToString("F2");
            if (ViewModel != null) ViewModel.AmbientIntensity = (float)e.NewValue;
        }

        private void AmbientColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (_isUpdating) return;
            if (ViewModel != null) ViewModel.AmbientColor = args.NewColor;
        }

        // Rim Light handlers
        private void RimIntensitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (RimIntensityValueText != null) RimIntensityValueText.Text = e.NewValue.ToString("F2");
            if (ViewModel != null) ViewModel.RimIntensity = (float)e.NewValue;
        }

        private void RimColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (_isUpdating) return;
            if (ViewModel != null) ViewModel.RimColor = args.NewColor;
        }

        // Back Light handlers
        private void BackIntensitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (BackIntensityValueText != null) BackIntensityValueText.Text = e.NewValue.ToString("F2");
            if (ViewModel != null) ViewModel.BackIntensity = (float)e.NewValue;
        }

        private void BackColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (_isUpdating) return;
            if (ViewModel != null) ViewModel.BackColor = args.NewColor;
        }
    }
}
