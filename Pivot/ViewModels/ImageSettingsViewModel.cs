using CommunityToolkit.Mvvm.ComponentModel;
using Pivot.Services;
using System;

namespace Pivot.ViewModels
{
    public partial class ImageSettingsViewModel : ObservableObject
    {
        private readonly ThemeSettingsService _themeSettings;

        // Selection Settings
        [ObservableProperty]
        private string _selectionColor;

        [ObservableProperty]
        private double _selectionOpacity;

        [ObservableProperty]
        private double _selectionBorderThickness;

        // Browser Settings
        [ObservableProperty]
        private int _thumbnailSize;

        [ObservableProperty]
        private int _decodePixelWidth;

        [ObservableProperty]
        private int _pageSize;

        // Preview Settings
        [ObservableProperty]
        private double _maxZoom;

        [ObservableProperty]
        private double _fixedDisplaySize;

        public ImageSettingsViewModel(ThemeSettingsService themeSettings)
        {
            _themeSettings = themeSettings ?? throw new ArgumentNullException(nameof(themeSettings));
            
            // Load current values - Selection
            SelectionColor = _themeSettings.ImageSelectionColor;
            SelectionOpacity = _themeSettings.ImageSelectionOpacity;
            SelectionBorderThickness = _themeSettings.ImageSelectionBorderThickness;
            
            // Load current values - Browser
            ThumbnailSize = _themeSettings.ThumbnailSize;
            DecodePixelWidth = _themeSettings.DecodePixelWidth;
            PageSize = _themeSettings.PageSize;
            
            // Load current values - Preview
            MaxZoom = _themeSettings.PreviewMaxZoom;
            FixedDisplaySize = _themeSettings.PreviewFixedDisplaySize;
        }

        // Selection Changed Handlers
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

        // Browser Changed Handlers
        partial void OnThumbnailSizeChanged(int value)
        {
            Utilities.SafeAsync.FireAndForget(
                _themeSettings.SetThumbnailSizeAsync(value),
                nameof(OnThumbnailSizeChanged));
        }

        partial void OnDecodePixelWidthChanged(int value)
        {
            Utilities.SafeAsync.FireAndForget(
                _themeSettings.SetDecodePixelWidthAsync(value),
                nameof(OnDecodePixelWidthChanged));
        }

        partial void OnPageSizeChanged(int value)
        {
            Utilities.SafeAsync.FireAndForget(
                _themeSettings.SetPageSizeAsync(value),
                nameof(OnPageSizeChanged));
        }

        // Preview Changed Handlers
        partial void OnMaxZoomChanged(double value)
        {
            Utilities.SafeAsync.FireAndForget(
                _themeSettings.SetPreviewMaxZoomAsync(value),
                nameof(OnMaxZoomChanged));
        }

        partial void OnFixedDisplaySizeChanged(double value)
        {
            Utilities.SafeAsync.FireAndForget(
                _themeSettings.SetPreviewFixedDisplaySizeAsync(value),
                nameof(OnFixedDisplaySizeChanged));
        }
    }
}
