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
        private readonly MaterialSettingsService _materialSettings;
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

        public MaterialSettingsViewModel(MaterialSettingsService materialSettings, IMessenger messenger)
        {
            _materialSettings = materialSettings ?? throw new ArgumentNullException(nameof(materialSettings));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

            LoadPresets();
            LoadCurrentSettings();
        }

        private void LoadPresets()
        {
            Presets.Clear();
            
            foreach (var preset in MaterialPreset.GetDefaultPresets())
            {
                Presets.Add(preset);
            }

            var customPresets = _materialSettings.GetMaterialPresets();
            foreach (var preset in customPresets.Where(p => p.IsCustom))
            {
                Presets.Add(preset);
            }
        }

        private void LoadCurrentSettings()
        {
            var mp = _materialSettings.GetMaterialParams();
            
            Albedo = Color.FromArgb(255, (byte)(mp.AlbedoR * 255), (byte)(mp.AlbedoG * 255), (byte)(mp.AlbedoB * 255));
            Metallic = mp.Metallic;
            Roughness = mp.Roughness;

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
            _ = _materialSettings.SetMaterialParamsAsync(
                Albedo.R / 255f, Albedo.G / 255f, Albedo.B / 255f,
                Metallic, Roughness);

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

            var customPresets = Presets.Where(p => p.IsCustom).ToList();
            await _materialSettings.SetMaterialPresetsAsync(customPresets);
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

            var customPresets = Presets.Where(p => p.IsCustom).ToList();
            await _materialSettings.SetMaterialPresetsAsync(customPresets);
        }

        [RelayCommand]
        private void ResetToDefault()
        {
            SelectedPreset = Presets.FirstOrDefault();
        }
    }
}
