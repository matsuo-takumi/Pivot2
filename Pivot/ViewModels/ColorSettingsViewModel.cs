using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Pivot.Services;
using Pivot.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace Pivot.ViewModels
{
    public partial class ColorSettingsViewModel : ObservableObject
    {
        private readonly ThemeSettingsService _themeSettings;
        private readonly ITextColorResourceManager _resourceManager;

        [ObservableProperty]
        private bool _isTextColorCustomizationEnabled;

        [ObservableProperty]
        private string _imageSelectionColor;

        [ObservableProperty]
        private double _imageSelectionOpacity;

        [ObservableProperty]
        private double _imageSelectionBorderThickness;

        public TextColorSettingsViewModel TextColorSettings { get; }
        public TextColorPresetViewModel PresetViewModel { get; }

        public ColorSettingsViewModel(
            ThemeSettingsService themeSettings,
            ITextColorResourceManager resourceManager,
            IPresetService<Models.TextColorPresetData> presetService,
            Microsoft.Extensions.Logging.ILogger<TextColorPresetViewModel>? presetLogger = null)
        {
            _themeSettings = themeSettings ?? throw new ArgumentNullException(nameof(themeSettings));
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            
            // Initialize lightweight properties first (fast, cached lookups)
            IsTextColorCustomizationEnabled = _themeSettings.IsTextColorCustomizationEnabled;
            ImageSelectionColor = _themeSettings.ImageSelectionColor;
            ImageSelectionOpacity = _themeSettings.ImageSelectionOpacity;
            ImageSelectionBorderThickness = _themeSettings.ImageSelectionBorderThickness;
            
            // Initialize TextColorSettings (entries will be loaded asynchronously after page loads)
            TextColorSettings = new TextColorSettingsViewModel(themeSettings, resourceManager);
            
            // Initialize PresetViewModel last (lightweight, just holds references)
            PresetViewModel = new TextColorPresetViewModel(presetService, TextColorSettings, presetLogger);
        }

        public async Task LoadEntriesAsync()
        {
            await TextColorSettings.LoadEntriesAsync();
        }

        partial void OnIsTextColorCustomizationEnabledChanged(bool value)
        {
            _ = _themeSettings.SetTextColorCustomizationEnabledAsync(value);
            // When disabled, restore default theme colors
            if (!value)
            {
                RestoreDefaultThemeColors();
            }
        }

        partial void OnImageSelectionColorChanged(string value)
        {
            _ = _themeSettings.SetImageSelectionColorAsync(value);
        }

        partial void OnImageSelectionOpacityChanged(double value)
        {
            _ = _themeSettings.SetImageSelectionOpacityAsync(value);
        }

        partial void OnImageSelectionBorderThicknessChanged(double value)
        {
            _ = _themeSettings.SetImageSelectionBorderThicknessAsync(value);
        }

        private void RestoreDefaultThemeColors()
        {
            // Restore all text colors to default theme colors
            _resourceManager.UpdateThemeColors();
        }

        public async Task ResetToDefaultsAsync()
        {
            // Reset individual text color roles to defaults
            foreach (var entry in TextColorSettings.Entries)
            {
                var definition = TextColorRoleDefinitions.Roles.FirstOrDefault(r => r.SettingKey == entry.SettingKey);
                if (definition != null)
                {
                    entry.SelectedColor = definition.DefaultColor;
                    // Apply the default color to the resource manager
                    _resourceManager.ApplyColor(entry.SettingKey, definition.DefaultColor);
                }
            }

            // Clear all text color overrides
            await _themeSettings.ClearAllTextColorOverridesAsync();
        }

    }
}


