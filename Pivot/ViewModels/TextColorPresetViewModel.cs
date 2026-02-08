using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Pivot.Engine.Models;
using Pivot.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Pivot.ViewModels
{
    /// <summary>
    /// テキストカラープリセット管理用のViewModel
    /// </summary>
    public partial class TextColorPresetViewModel : ObservableObject
    {
        private readonly IPresetService<TextColorPresetData> _presetService;
        private readonly TextColorSettingsViewModel _textColorSettings;
        private readonly ILogger<TextColorPresetViewModel>? _logger;

        public ObservableCollection<Preset<TextColorPresetData>> Presets { get; } = new();

        private Preset<TextColorPresetData>? _selectedPreset;
        public Preset<TextColorPresetData>? SelectedPreset
        {
            get => _selectedPreset;
            set => SetProperty(ref _selectedPreset, value);
        }

        [ObservableProperty]
        private bool _isLoading;

        public TextColorPresetViewModel(
            IPresetService<TextColorPresetData> presetService,
            TextColorSettingsViewModel textColorSettings,
            ILogger<TextColorPresetViewModel>? logger = null)
        {
            _presetService = presetService ?? throw new ArgumentNullException(nameof(presetService));
            _textColorSettings = textColorSettings ?? throw new ArgumentNullException(nameof(textColorSettings));
            _logger = logger;
        }

        /// <summary>
        /// プリセット一覧を読み込む
        /// </summary>
        public async Task LoadPresetsAsync()
        {
            try
            {
                IsLoading = true;
                var presets = await _presetService.GetAllPresetsAsync();
                
                Presets.Clear();
                foreach (var preset in presets.OrderByDescending(p => p.UpdatedAt))
                {
                    Presets.Add(preset);
                }

                _logger?.LogInformation("Loaded {Count} text color presets", Presets.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load presets");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 現在の設定をプリセットとして保存
        /// </summary>
        [RelayCommand]
        public async Task SaveCurrentAsPresetAsync(string? presetName = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(presetName))
                {
                    // UIから名前を入力してもらう必要がある場合は、ここでダイアログを表示
                    // 今回は簡易的にタイムスタンプを使用
                    presetName = $"Preset {DateTime.Now:yyyy-MM-dd HH:mm}";
                }

                // 現在の設定を収集
                var data = new TextColorPresetData
                {
                    ColorOverrides = _textColorSettings.Entries.ToDictionary(
                        e => e.SettingKey,
                        e => e.HexValue)
                };

                var preset = new Preset<TextColorPresetData>
                {
                    Name = presetName,
                    Data = data
                };

                await _presetService.SavePresetAsync(preset);
                await LoadPresetsAsync();
                
                _logger?.LogInformation("Saved preset: {Name}", presetName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save preset");
            }
        }

        /// <summary>
        /// 選択されたプリセットを適用
        /// </summary>
        [RelayCommand]
        public Task ApplyPresetAsync(Preset<TextColorPresetData>? preset)
        {
            if (preset == null || preset.Data == null) return Task.CompletedTask;

            try
            {
                foreach (var entry in _textColorSettings.Entries)
                {
                    if (preset.Data.ColorOverrides.TryGetValue(entry.SettingKey, out var hexValue))
                    {
                        entry.HexValue = hexValue;
                    }
                }

                SelectedPreset = preset;
                _logger?.LogInformation("Applied preset: {Name}", preset.Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to apply preset: {Id}", preset?.Id);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// プリセットを削除
        /// </summary>
        [RelayCommand]
        public async Task DeletePresetAsync(Preset<TextColorPresetData> preset)
        {
            if (preset == null) return;

            try
            {
                var deleted = await _presetService.DeletePresetAsync(preset.Id);
                if (deleted)
                {
                    if (SelectedPreset?.Id == preset.Id)
                    {
                        SelectedPreset = null;
                    }
                    await LoadPresetsAsync();
                    _logger?.LogInformation("Deleted preset: {Name}", preset.Name);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to delete preset: {Id}", preset.Id);
            }
        }

        /// <summary>
        /// プリセット名を更新
        /// </summary>
        [RelayCommand]
        private async Task UpdatePresetNameAsync((Preset<TextColorPresetData> Preset, string NewName) args)
        {
            if (args.Preset == null || string.IsNullOrWhiteSpace(args.NewName)) return;

            try
            {
                // 名前の重複チェック
                var nameExists = await _presetService.IsNameExistsAsync(args.NewName, args.Preset.Id);
                if (nameExists)
                {
                    _logger?.LogWarning("Preset name already exists: {Name}", args.NewName);
                    return;
                }

                args.Preset.Name = args.NewName;
                args.Preset.UpdatedAt = DateTime.UtcNow;
                await _presetService.SavePresetAsync(args.Preset);
                await LoadPresetsAsync();
                
                _logger?.LogInformation("Updated preset name: {Id} -> {Name}", args.Preset.Id, args.NewName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to update preset name: {Id}", args.Preset.Id);
            }
        }
    }
}

