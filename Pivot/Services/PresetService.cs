using Microsoft.Extensions.Logging;
using Pivot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pivot.Services
{
    /// <summary>
    /// 汎用的なプリセットサービスの実装
    /// </summary>
    public class PresetService<T> : IPresetService<T>
    {
        private readonly ILogger<PresetService<T>> _logger;
        private readonly ISettingsStore _settingsStore;
        private readonly string _presetKeyPrefix;
        private readonly JsonSerializerOptions _jsonOptions;

        public PresetService(ILogger<PresetService<T>> logger, ISettingsStore settingsStore, string presetKeyPrefix)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _presetKeyPrefix = presetKeyPrefix ?? throw new ArgumentNullException(nameof(presetKeyPrefix));
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<IReadOnlyList<Preset<T>>> GetAllPresetsAsync()
        {
            try
            {
                var presetsJson = await _settingsStore.GetAsync(_presetKeyPrefix);
                if (string.IsNullOrWhiteSpace(presetsJson))
                {
                    return Array.Empty<Preset<T>>();
                }

                var presetList = JsonSerializer.Deserialize<List<Preset<T>>>(presetsJson, _jsonOptions);
                return (IReadOnlyList<Preset<T>>)(presetList ?? new List<Preset<T>>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load presets from key: {Key}", _presetKeyPrefix);
                return Array.Empty<Preset<T>>();
            }
        }

        public async Task<Preset<T>> SavePresetAsync(Preset<T> preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));

            try
            {
                var allPresets = (await GetAllPresetsAsync()).ToList();
                
                // 既存のプリセットを更新または新規追加
                var existingIndex = allPresets.FindIndex(p => p.Id == preset.Id);
                if (existingIndex >= 0)
                {
                    preset.UpdatedAt = DateTime.UtcNow;
                    allPresets[existingIndex] = preset;
                }
                else
                {
                    preset.CreatedAt = DateTime.UtcNow;
                    preset.UpdatedAt = DateTime.UtcNow;
                    allPresets.Add(preset);
                }

                var json = JsonSerializer.Serialize(allPresets, _jsonOptions);
                await _settingsStore.UpsertAsync(_presetKeyPrefix, json);
                
                _logger.LogInformation("Preset saved: {Id}, Name: {Name}", preset.Id, preset.Name);
                return preset;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save preset: {Id}", preset.Id);
                throw;
            }
        }

        public async Task<bool> DeletePresetAsync(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId)) return false;

            try
            {
                var allPresets = (await GetAllPresetsAsync()).ToList();
                var removed = allPresets.RemoveAll(p => p.Id == presetId) > 0;

                if (removed)
                {
                    var json = JsonSerializer.Serialize(allPresets, _jsonOptions);
                    await _settingsStore.UpsertAsync(_presetKeyPrefix, json);
                    _logger.LogInformation("Preset deleted: {Id}", presetId);
                }

                return removed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete preset: {Id}", presetId);
                return false;
            }
        }

        public async Task<Preset<T>?> GetPresetAsync(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId)) return null;

            try
            {
                var allPresets = await GetAllPresetsAsync();
                return allPresets.FirstOrDefault(p => p.Id == presetId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get preset: {Id}", presetId);
                return null;
            }
        }

        public async Task<bool> IsNameExistsAsync(string name, string? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            try
            {
                var allPresets = await GetAllPresetsAsync();
                return allPresets.Any(p => 
                    string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) &&
                    (excludeId == null || p.Id != excludeId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check preset name existence: {Name}", name);
                return false;
            }
        }
    }
}

