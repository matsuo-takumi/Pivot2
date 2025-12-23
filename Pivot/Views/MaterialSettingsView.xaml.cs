using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.Models;
using Pivot.Services;
using Pivot.ViewModels;
using Pivot.Messages;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI;

namespace Pivot.Views
{
    /// <summary>
    /// Material settings UserControl for the 3D viewport
    /// </summary>
    public sealed partial class MaterialSettingsView : UserControl
    {
        private readonly SettingsService _settings;
        private readonly IMessenger _messenger;
        private bool _isLoading = true;

        public ObservableCollection<MaterialPreset> Presets { get; } = new();

        public MaterialSettingsView()
        {
            this.InitializeComponent();

            _settings = App.Current.Services.GetRequiredService<SettingsService>();
            _messenger = App.Current.Services.GetRequiredService<IMessenger>();

            this.Loaded += MaterialSettingsView_Loaded;
        }

        private void MaterialSettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPresets();
            LoadCurrentSettings();
            _isLoading = false;
        }

        private void LoadPresets()
        {
            Presets.Clear();
            foreach (var preset in _settings.GetMaterialPresets())
            {
                Presets.Add(preset);
            }
            PresetComboBox.ItemsSource = Presets;
        }

        private void LoadCurrentSettings()
        {
            var (r, g, b, metallic, roughness) = _settings.GetMaterialParams();

            AlbedoColorPicker.Color = Color.FromArgb(255, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
            MetallicSlider.Value = metallic;
            RoughnessSlider.Value = roughness;

            UpdateValueTexts();

            // Find matching preset
            var currentAlbedo = AlbedoColorPicker.Color;
            var matching = Presets.FirstOrDefault(p =>
                Math.Abs(p.Albedo.R - currentAlbedo.R) < 5 &&
                Math.Abs(p.Albedo.G - currentAlbedo.G) < 5 &&
                Math.Abs(p.Albedo.B - currentAlbedo.B) < 5 &&
                Math.Abs(p.Metallic - metallic) < 0.05f &&
                Math.Abs(p.Roughness - roughness) < 0.05f);

            if (matching != null)
            {
                PresetComboBox.SelectedItem = matching;
            }
        }

        private void UpdateValueTexts()
        {
            MetallicValueText.Text = $"{MetallicSlider.Value:F2}";
            RoughnessValueText.Text = $"{RoughnessSlider.Value:F2}";
        }

        private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading || PresetComboBox.SelectedItem is not MaterialPreset preset) return;

            _isLoading = true;
            AlbedoColorPicker.Color = preset.Albedo;
            MetallicSlider.Value = preset.Metallic;
            RoughnessSlider.Value = preset.Roughness;
            UpdateValueTexts();
            _isLoading = false;

            ApplyMaterialSettings();
        }

        private void AlbedoColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (_isLoading) return;
            ApplyMaterialSettings();
        }

        private void MetallicSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_isLoading) return;
            MetallicValueText.Text = $"{e.NewValue:F2}";
            ApplyMaterialSettings();
        }

        private void RoughnessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_isLoading) return;
            RoughnessValueText.Text = $"{e.NewValue:F2}";
            ApplyMaterialSettings();
        }

        private void ApplyMaterialSettings()
        {
            var color = AlbedoColorPicker.Color;
            float r = color.R / 255f;
            float g = color.G / 255f;
            float b = color.B / 255f;
            float metallic = (float)MetallicSlider.Value;
            float roughness = (float)RoughnessSlider.Value;

            _ = _settings.SetMaterialParamsAsync(r, g, b, metallic, roughness);
            _messenger.Send(new SettingsChangedMessage("MaterialParams"));
        }

        private async void SavePresetButton_Click(object sender, RoutedEventArgs e)
        {
            var name = NewPresetNameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;

            var newPreset = new MaterialPreset
            {
                Name = name,
                Albedo = AlbedoColorPicker.Color,
                Metallic = (float)MetallicSlider.Value,
                Roughness = (float)RoughnessSlider.Value,
                IsCustom = true
            };

            Presets.Add(newPreset);
            PresetComboBox.SelectedItem = newPreset;
            NewPresetNameBox.Text = string.Empty;

            // Save custom presets
            var customPresets = Presets.Where(p => p.IsCustom).ToList();
            await _settings.SetMaterialPresetsAsync(customPresets);
        }
    }
}
