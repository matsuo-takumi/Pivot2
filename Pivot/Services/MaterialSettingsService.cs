using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pivot.Models;

namespace Pivot.Services
{
    /// <summary>
    /// Material/Lighting設定の管理サービス。
    /// </summary>
    public class MaterialSettingsService
    {
        private readonly ILogger<MaterialSettingsService> _logger;
        private readonly ISettingsStore _settingsStore;

        // In-memory cache
        private MaterialParams _materialParams = new();
        private List<MaterialPreset> _customMaterialPresets = new();
        private LightingPreset? _currentLightingState;
        private MaterialPreset? _currentMaterialState;
        
        // Key bindings
        private string _assetLitShortcut = "Alt+1";
        private string _assetDepthShortcut = "Alt+2";
        private string _assetWorldNormalShortcut = "Alt+3";

        public MaterialSettingsService(
            ILogger<MaterialSettingsService> logger,
            ISettingsStore settingsStore)
        {
            _logger = logger;
            _settingsStore = settingsStore;
        }

        public async Task LoadAsync()
        {
            _logger.LogInformation("MaterialSettingsService: Loading...");
            try
            {
                // Material params
                var paramsJson = await _settingsStore.GetAsync("Material.Params");
                if (!string.IsNullOrEmpty(paramsJson))
                {
                    _materialParams = JsonSerializer.Deserialize<MaterialParams>(paramsJson) ?? new();
                }

                // Custom presets
                var presetsJson = await _settingsStore.GetAsync("Material.CustomPresets");
                if (!string.IsNullOrEmpty(presetsJson))
                {
                    _customMaterialPresets = JsonSerializer.Deserialize<List<MaterialPreset>>(presetsJson) ?? new();
                }

                // Key shortcuts
                _assetLitShortcut = await _settingsStore.GetAsync("KeyConfig.AssetLit") ?? "Alt+1";
                _assetDepthShortcut = await _settingsStore.GetAsync("KeyConfig.AssetDepth") ?? "Alt+2";
                _assetWorldNormalShortcut = await _settingsStore.GetAsync("KeyConfig.AssetWorldNormal") ?? "Alt+3";

                _logger.LogInformation("MaterialSettingsService: Loaded");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MaterialSettingsService: Failed to load, using defaults.");
            }
        }

        // =============== Material Params ===============

        public MaterialParams GetMaterialParams() => _materialParams;

        public async Task SetMaterialParamsAsync(MaterialParams? mp)
        {
            _materialParams = mp ?? new MaterialParams();
            try
            {
                var json = JsonSerializer.Serialize(_materialParams);
                await _settingsStore.UpsertAsync("Material.Params", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MaterialSettingsService: Failed to persist Material params.");
            }
        }

        public Task SetMaterialParamsAsync(float r, float g, float b, float metallic, float roughness)
        {
            var mp = new MaterialParams
            {
                AlbedoR = r,
                AlbedoG = g,
                AlbedoB = b,
                Metallic = metallic,
                Roughness = roughness
            };
            return SetMaterialParamsAsync(mp);
        }

        // =============== Material Presets ===============

        public List<MaterialPreset> GetMaterialPresets()
        {
            var presets = MaterialPreset.GetDefaultPresets();
            presets.AddRange(_customMaterialPresets);
            return presets;
        }

        public async Task SetMaterialPresetsAsync(List<MaterialPreset> customPresets)
        {
            _customMaterialPresets = customPresets ?? new List<MaterialPreset>();
            try
            {
                var json = JsonSerializer.Serialize(_customMaterialPresets);
                await _settingsStore.UpsertAsync("Material.CustomPresets", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MaterialSettingsService: Failed to persist Material presets.");
            }
        }

        // =============== Lighting State ===============

        public async Task<LightingPreset?> GetSavedLightingStateAsync()
        {
            try
            {
                var json = await _settingsStore.GetAsync("Viewport.LightingState");
                if (!string.IsNullOrWhiteSpace(json))
                {
                    return JsonSerializer.Deserialize<LightingPreset>(json);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MaterialSettingsService: Failed to load lighting state.");
            }
            return null;
        }

        public async Task SaveLightingStateAsync(LightingPreset state)
        {
            try
            {
                var json = JsonSerializer.Serialize(state);
                await _settingsStore.UpsertAsync("Viewport.LightingState", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MaterialSettingsService: Failed to save lighting state.");
            }
        }

        // =============== Lighting Presets ===============

        public async Task<List<LightingPreset>> GetLightingPresetsAsync()
        {
            try
            {
                var json = await _settingsStore.GetAsync("Viewport.LightingPresets");
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var presets = JsonSerializer.Deserialize<List<LightingPreset>>(json);
                    if (presets != null && presets.Count > 0)
                        return presets;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MaterialSettingsService: Failed to load lighting presets.");
            }
            return LightingPreset.GetDefaultPresets();
        }

        public async Task SaveLightingPresetsAsync(List<LightingPreset> presets)
        {
            try
            {
                var json = JsonSerializer.Serialize(presets);
                await _settingsStore.UpsertAsync("Viewport.LightingPresets", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MaterialSettingsService: Failed to save lighting presets.");
            }
        }

        // =============== Material State ===============

        public async Task<MaterialPreset?> GetSavedMaterialStateAsync()
        {
            try
            {
                var json = await _settingsStore.GetAsync("Viewport.MaterialState");
                if (!string.IsNullOrWhiteSpace(json))
                {
                    return JsonSerializer.Deserialize<MaterialPreset>(json);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MaterialSettingsService: Failed to load material state.");
            }
            return null;
        }

        public async Task SaveMaterialStateAsync(MaterialPreset state)
        {
            try
            {
                var json = JsonSerializer.Serialize(state);
                await _settingsStore.UpsertAsync("Viewport.MaterialState", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MaterialSettingsService: Failed to save material state.");
            }
        }

        public async Task<List<MaterialPreset>> GetMaterialPresetsAsync()
        {
            try
            {
                var json = await _settingsStore.GetAsync("Viewport.MaterialPresets");
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var presets = JsonSerializer.Deserialize<List<MaterialPreset>>(json);
                    if (presets != null && presets.Count > 0)
                        return presets;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MaterialSettingsService: Failed to load material presets.");
            }
            return MaterialPreset.GetDefaultPresets();
        }

        public async Task SaveMaterialPresetsAsync(List<MaterialPreset> presets)
        {
            try
            {
                var json = JsonSerializer.Serialize(presets);
                await _settingsStore.UpsertAsync("Viewport.MaterialPresets", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MaterialSettingsService: Failed to save material presets.");
            }
        }

        // =============== Key Shortcuts ===============

        public string GetAssetLitShortcut() => _assetLitShortcut;
        public async Task SetAssetLitShortcutAsync(string shortcut)
        {
            _assetLitShortcut = shortcut ?? "Alt+1";
            try { await _settingsStore.UpsertAsync("KeyConfig.AssetLit", _assetLitShortcut); }
            catch (Exception ex) { _logger.LogWarning(ex, "MaterialSettingsService: Failed to persist KeyConfig.AssetLit."); }
        }

        public string GetAssetDepthShortcut() => _assetDepthShortcut;
        public async Task SetAssetDepthShortcutAsync(string shortcut)
        {
            _assetDepthShortcut = shortcut ?? "Alt+2";
            try { await _settingsStore.UpsertAsync("KeyConfig.AssetDepth", _assetDepthShortcut); }
            catch (Exception ex) { _logger.LogWarning(ex, "MaterialSettingsService: Failed to persist KeyConfig.AssetDepth."); }
        }

        public string GetAssetWorldNormalShortcut() => _assetWorldNormalShortcut;
        public async Task SetAssetWorldNormalShortcutAsync(string shortcut)
        {
            _assetWorldNormalShortcut = shortcut ?? "Alt+3";
            try { await _settingsStore.UpsertAsync("KeyConfig.AssetWorldNormal", _assetWorldNormalShortcut); }
            catch (Exception ex) { _logger.LogWarning(ex, "MaterialSettingsService: Failed to persist KeyConfig.AssetWorldNormal."); }
        }
    }

    /// <summary>
    /// Material params (placeholder - may need to match existing model)
    /// </summary>
    public class MaterialParams
    {
        public float AlbedoR { get; set; } = 0.7f;
        public float AlbedoG { get; set; } = 0.7f;
        public float AlbedoB { get; set; } = 0.7f;
        public float Metallic { get; set; } = 0.5f;
        public float Roughness { get; set; } = 0.5f;
        public float AmbientOcclusion { get; set; } = 1.0f;
    }
}
