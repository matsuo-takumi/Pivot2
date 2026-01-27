using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Pivot.Services;
using Pivot.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace Pivot.ViewModels
{
    public partial class CodeColorSettingsViewModel : ObservableObject
    {
        private readonly ThemeSettingsService _themeSettings;

        [ObservableProperty]
        private string _selectedTheme;

        [ObservableProperty]
        private string _backgroundColor;

        [ObservableProperty]
        private string _textColor;

        [ObservableProperty]
        private bool _useCustomColors;

        [ObservableProperty]
        private SolidColorBrush _backgroundPreviewBrush;

        [ObservableProperty]
        private SolidColorBrush _textPreviewBrush;

        public List<MonacoThemeOption> AvailableThemes { get; } = new()
        {
            new MonacoThemeOption { Name = "Dark (Visual Studio)", Value = "vs-dark" },
            new MonacoThemeOption { Name = "Light (Visual Studio)", Value = "vs-light" },
            new MonacoThemeOption { Name = "High Contrast", Value = "hc-black" }
        };

        public CodeColorSettingsViewModel(ThemeSettingsService themeSettings)
        {
            _themeSettings = themeSettings ?? throw new ArgumentNullException(nameof(themeSettings));

            // Load current settings
            _selectedTheme = _themeSettings.CodeEditorTheme;
            _backgroundColor = _themeSettings.CodeBackgroundColor;
            _textColor = _themeSettings.CodeTextColor;
            _useCustomColors = _themeSettings.UseCustomCodeColors;

            // Initialize preview brushes
            UpdatePreviewBrushes();
        }

        partial void OnSelectedThemeChanged(string value)
        {
            SafeAsync.FireAndForget(
                _themeSettings.SetCodeEditorThemeAsync(value),
                nameof(OnSelectedThemeChanged));
        }

        partial void OnBackgroundColorChanged(string value)
        {
            UpdatePreviewBrushes();
            SafeAsync.FireAndForget(
                _themeSettings.SetCodeBackgroundColorAsync(value),
                nameof(OnBackgroundColorChanged));
        }

        partial void OnTextColorChanged(string value)
        {
            UpdatePreviewBrushes();
            SafeAsync.FireAndForget(
                _themeSettings.SetCodeTextColorAsync(value),
                nameof(OnTextColorChanged));
        }

        partial void OnUseCustomColorsChanged(bool value)
        {
            SafeAsync.FireAndForget(
                _themeSettings.SetUseCustomCodeColorsAsync(value),
                nameof(OnUseCustomColorsChanged));
        }

        private void UpdatePreviewBrushes()
        {
            BackgroundPreviewBrush = CreateBrushFromHex(_backgroundColor);
            TextPreviewBrush = CreateBrushFromHex(_textColor);
        }

        private SolidColorBrush CreateBrushFromHex(string hex)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex))
                    return new SolidColorBrush(Colors.Transparent);

                hex = hex.Trim();
                if (!hex.StartsWith("#"))
                    hex = "#" + hex;

                // Parse hex color
                if (hex.Length == 7) // #RRGGBB
                {
                    var r = Convert.ToByte(hex.Substring(1, 2), 16);
                    var g = Convert.ToByte(hex.Substring(3, 2), 16);
                    var b = Convert.ToByte(hex.Substring(5, 2), 16);
                    return new SolidColorBrush(Color.FromArgb(255, r, g, b));
                }
                else if (hex.Length == 9) // #AARRGGBB
                {
                    var a = Convert.ToByte(hex.Substring(1, 2), 16);
                    var r = Convert.ToByte(hex.Substring(3, 2), 16);
                    var g = Convert.ToByte(hex.Substring(5, 2), 16);
                    var b = Convert.ToByte(hex.Substring(7, 2), 16);
                    return new SolidColorBrush(Color.FromArgb(a, r, g, b));
                }
            }
            catch { }

            return new SolidColorBrush(Colors.Transparent);
        }

        public class MonacoThemeOption
        {
            public string Name { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
        }
    }
}
