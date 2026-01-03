using CommunityToolkit.Mvvm.ComponentModel;
using Pivot.Services;
using System;

namespace Pivot.ViewModels
{
    public partial class ImageSettingsViewModel : ObservableObject
    {
        private readonly ThemeSettingsService _themeSettings;

        [ObservableProperty]
        private string _selectionColor;

        [ObservableProperty]
        private double _selectionOpacity;

        [ObservableProperty]
        private double _selectionBorderThickness;

        public ImageSettingsViewModel(ThemeSettingsService themeSettings)
        {
            _themeSettings = themeSettings ?? throw new ArgumentNullException(nameof(themeSettings));
            
            // Load current values
            SelectionColor = _themeSettings.ImageSelectionColor;
            SelectionOpacity = _themeSettings.ImageSelectionOpacity;
            SelectionBorderThickness = _themeSettings.ImageSelectionBorderThickness;
        }

        partial void OnSelectionColorChanged(string value)
        {
            Utilities.SafeAsync.FireAndForget(
                _themeSettings.SetImageSelectionColorAsync(value),
                nameof(OnSelectionColorChanged));
        }

        partial void OnSelectionOpacityChanged(double value)
        {
            Utilities.SafeAsync.FireAndForget(
                _themeSettings.SetImageSelectionOpacityAsync(value),
                nameof(OnSelectionOpacityChanged));
        }

        partial void OnSelectionBorderThicknessChanged(double value)
        {
            Utilities.SafeAsync.FireAndForget(
                _themeSettings.SetImageSelectionBorderThicknessAsync(value),
                nameof(OnSelectionBorderThicknessChanged));
        }
    }
}
