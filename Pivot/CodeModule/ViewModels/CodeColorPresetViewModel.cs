using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pivot.Engine.Models;
using Pivot.Models;
using Pivot.Services;
using Pivot.CodeModule.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Pivot.CodeModule.ViewModels
{
    public partial class CodeColorPresetViewModel : ObservableObject
    {
        private readonly CodeSettingsService _settingsService;
        private readonly IPresetService<CodeColorPresetData> _presetService;
        private readonly ILogger<CodeColorPresetViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<Preset<CodeColorPresetData>> _presets = new();

        [ObservableProperty]
        private Preset<CodeColorPresetData>? _selectedPreset;

        public CodeColorPresetViewModel(
            CodeSettingsService settingsService,
            IPresetService<CodeColorPresetData> presetService,
            ILogger<CodeColorPresetViewModel> logger)
        {
            _settingsService = settingsService;
            _presetService = presetService;
            _logger = logger;
        }

        public async Task LoadPresetsAsync()
        {
            try
            {
                var presets = await _presetService.GetAllPresetsAsync();
                Presets.Clear();
                foreach (var preset in presets.OrderByDescending(p => p.UpdatedAt))
                {
                    Presets.Add(preset);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load code color presets.");
            }
        }

        [RelayCommand]
        public async Task SaveCurrentAsPresetAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            try
            {
                var data = _settingsService.CreatePresetData();
                var preset = new Preset<CodeColorPresetData>
                {
                    Name = name,
                    Data = data
                };

                var saved = await _presetService.SavePresetAsync(preset);
                
                // Add to list or update existing
                var existing = Presets.FirstOrDefault(p => p.Id == saved.Id);
                if (existing != null)
                {
                    var index = Presets.IndexOf(existing);
                    Presets[index] = saved;
                }
                else
                {
                    Presets.Insert(0, saved);
                }
                
                SelectedPreset = saved;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save code color preset.");
            }
        }

        [RelayCommand]
        public async Task ApplyPresetAsync(Preset<CodeColorPresetData>? preset)
        {
            if (preset == null) return;

            try
            {
                await _settingsService.ApplyPresetAsync(preset.Data);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to apply code color preset.");
            }
        }

        [RelayCommand]
        public async Task DeletePresetAsync(Preset<CodeColorPresetData>? preset)
        {
            if (preset == null) return;

            try
            {
                await _presetService.DeletePresetAsync(preset.Id);
                Presets.Remove(preset);
                if (SelectedPreset == preset)
                {
                    SelectedPreset = null;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to delete code color preset.");
            }
        }
    }
}
