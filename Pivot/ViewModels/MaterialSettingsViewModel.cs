using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Models;
using Pivot.Services;
using Pivot.Messages;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace Pivot.ViewModels
{
    /// <summary>
    /// ViewModel for Material settings in the 3D viewport
    /// </summary>
    public partial class MaterialSettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settings;
        private readonly IMessenger _messenger;

        public ObservableCollection<MaterialPreset> Presets { get; } = new();

        [ObservableProperty]
        private MaterialPreset? _selectedPreset;

        [ObservableProperty]
        private Color _albedo = Color.FromArgb(255, 180, 180, 180);

        [ObservableProperty]
        private float _metallic = 0.0f;

        [ObservableProperty]
        private float _roughness = 0.5f;

        [ObservableProperty]
        private bool _isCustomMode;

        public MaterialSettingsViewModel(SettingsService settings, IMessenger messenger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

            LoadPresets();
            LoadCurrentSettings();
        }

        private void LoadPresets()
        {
            Presets.Clear();
            
            // Add default presets
            foreach (var preset in MaterialPreset.GetDefaultPresets())
            {
                Presets.Add(preset);
            }

            // Load custom presets from settings
            var customPresets = _settings.GetMaterialPresets();
            foreach (var preset in customPresets.Where(p => p.IsCustom))
            {
                Presets.Add(preset);
            }
        }

        private void LoadCurrentSettings()
        {
            var settings = _settings.GetUserSettings();
            
            // Load current material values
            Albedo = Color.FromArgb(255, 
                (byte)(settings.MaterialAlbedoR * 255),
                (byte)(settings.MaterialAlbedoG * 255),
                (byte)(settings.MaterialAlbedoB * 255));
            Metallic = settings.MaterialMetallic;
            Roughness = settings.MaterialRoughness;

            // Find matching preset if any
            var matching = Presets.FirstOrDefault(p => 
                Math.Abs(p.Albedo.R - Albedo.R) < 5 &&
                Math.Abs(p.Albedo.G - Albedo.G) < 5 &&
                Math.Abs(p.Albedo.B - Albedo.B) < 5 &&
                Math.Abs(p.Metallic - Metallic) < 0.05f &&
                Math.Abs(p.Roughness - Roughness) < 0.05f);

            if (matching != null)
            {
                SelectedPreset = matching;
                IsCustomMode = false;
            }
            else
            {
                IsCustomMode = true;
            }
        }

        partial void OnSelectedPresetChanged(MaterialPreset? value)
        {
            if (value == null) return;

            IsCustomMode = false;
            Albedo = value.Albedo;
            Metallic = value.Metallic;
            Roughness = value.Roughness;

            ApplyMaterialSettings();
        }

        partial void OnAlbedoChanged(Color value)
        {
            if (!IsCustomMode) IsCustomMode = true;
            ApplyMaterialSettings();
        }

        partial void OnMetallicChanged(float value)
        {
            if (!IsCustomMode) IsCustomMode = true;
            ApplyMaterialSettings();
        }

        partial void OnRoughnessChanged(float value)
        {
            if (!IsCustomMode) IsCustomMode = true;
            ApplyMaterialSettings();
        }

        private void ApplyMaterialSettings()
        {
            // Save to settings
            _ = _settings.SetMaterialParamsAsync(
                Albedo.R / 255f, Albedo.G / 255f, Albedo.B / 255f,
                Metallic, Roughness);

            // Notify renderer
            _messenger.Send(new SettingsChangedMessage("MaterialParams"));
        }

        [RelayCommand]
        private async Task SaveAsPresetAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            var newPreset = new MaterialPreset
            {
                Name = name,
                Albedo = Albedo,
                Metallic = Metallic,
                Roughness = Roughness,
                IsCustom = true
            };

            Presets.Add(newPreset);
            SelectedPreset = newPreset;

            // Save to settings
            var customPresets = Presets.Where(p => p.IsCustom).ToList();
            await _settings.SetMaterialPresetsAsync(customPresets);
        }

        [RelayCommand]
        private async Task DeletePresetAsync(MaterialPreset? preset)
        {
            if (preset == null || !preset.IsCustom) return;

            Presets.Remove(preset);
            
            if (SelectedPreset == preset)
            {
                SelectedPreset = Presets.FirstOrDefault();
            }

            // Save to settings
            var customPresets = Presets.Where(p => p.IsCustom).ToList();
            await _settings.SetMaterialPresetsAsync(customPresets);
        }

        [RelayCommand]
        private void ResetToDefault()
        {
            SelectedPreset = Presets.FirstOrDefault();
        }
    }
}
