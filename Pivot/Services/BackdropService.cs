using System;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Pivot.Models;
using WinRT;

namespace Pivot.Services
{
    /// <summary>
    /// バックドロップ管理サービスの実装。
    /// Mica/Acrylic/Overlay/Noneの切り替えを管理する。
    /// </summary>
    public class BackdropService : IBackdropService, IDisposable
    {
        private readonly ThemeSettingsService _themeSettings;
        private MicaController? _micaController;
        private DesktopAcrylicController? _acrylicController;
        private SystemBackdropConfiguration? _configurationSource;
        private Window? _currentWindow;

        public BackdropType CurrentBackdropType { get; private set; } = BackdropType.None;

        public BackdropService(ThemeSettingsService themeSettings)
        {
            _themeSettings = themeSettings;
        }

        public void SetBackdrop(Window window, BackdropType type, FrameworkElement? rootElement, FrameworkElement? titleBar)
        {
            _currentWindow = window;
            CurrentBackdropType = type;

            // Dispose existing controllers
            DisposeControllers();

            if (type == BackdropType.None)
            {
                window.SystemBackdrop = null;
                SetSolidBackground(rootElement, titleBar);
                return;
            }

            if (type == BackdropType.Overlay)
            {
                window.SystemBackdrop = null;
                SetOverlayBackground(rootElement, titleBar);
                return;
            }

            // For Mica/Acrylic, set up configuration source
            _configurationSource = new SystemBackdropConfiguration();
            _configurationSource.IsInputActive = true;

            SetTransparentBackground(rootElement, titleBar);

            switch (type)
            {
                case BackdropType.Mica:
                    if (MicaController.IsSupported())
                    {
                        _micaController = new MicaController { Kind = MicaKind.Base };
                        _micaController.AddSystemBackdropTarget(window.As<ICompositionSupportsSystemBackdrop>());
                        _micaController.SetSystemBackdropConfiguration(_configurationSource);
                        window.SystemBackdrop = null;
                    }
                    break;
                case BackdropType.MicaAlt:
                    if (MicaController.IsSupported())
                    {
                        _micaController = new MicaController { Kind = MicaKind.BaseAlt };
                        _micaController.AddSystemBackdropTarget(window.As<ICompositionSupportsSystemBackdrop>());
                        _micaController.SetSystemBackdropConfiguration(_configurationSource);
                        window.SystemBackdrop = null;
                    }
                    break;
                case BackdropType.AcrylicThin:
                    if (DesktopAcrylicController.IsSupported())
                    {
                        _acrylicController = new DesktopAcrylicController { Kind = DesktopAcrylicKind.Thin };
                        _acrylicController.AddSystemBackdropTarget(window.As<ICompositionSupportsSystemBackdrop>());
                        _acrylicController.SetSystemBackdropConfiguration(_configurationSource);
                        window.SystemBackdrop = null;
                    }
                    break;
                case BackdropType.Acrylic:
                    if (DesktopAcrylicController.IsSupported())
                    {
                        _acrylicController = new DesktopAcrylicController { Kind = DesktopAcrylicKind.Base };
                        _acrylicController.AddSystemBackdropTarget(window.As<ICompositionSupportsSystemBackdrop>());
                        _acrylicController.SetSystemBackdropConfiguration(_configurationSource);
                        window.SystemBackdrop = null;
                    }
                    break;
                default:
                    window.SystemBackdrop = null;
                    SetTransparentBackground(rootElement, titleBar);
                    break;
            }
        }

        public void UpdateTheme(ElementTheme theme)
        {
            if (_configurationSource == null) return;
            _configurationSource.Theme = theme switch
            {
                ElementTheme.Dark => SystemBackdropTheme.Dark,
                ElementTheme.Light => SystemBackdropTheme.Light,
                _ => SystemBackdropTheme.Default
            };
        }

        public void SetInputActive(bool isActive)
        {
            if (_configurationSource != null)
            {
                _configurationSource.IsInputActive = isActive;
            }
        }

        public void Dispose()
        {
            DisposeControllers();
            _configurationSource = null;
            _currentWindow = null;
        }

        private void DisposeControllers()
        {
            _micaController?.Dispose();
            _micaController = null;
            _acrylicController?.Dispose();
            _acrylicController = null;
        }

        private void SetTransparentBackground(FrameworkElement? rootElement, FrameworkElement? titleBar)
        {
            var brush = new SolidColorBrush(Colors.Transparent);
            if (rootElement is Microsoft.UI.Xaml.Controls.Panel rootPanel) rootPanel.Background = brush;
            if (titleBar is Microsoft.UI.Xaml.Controls.Panel titlePanel) titlePanel.Background = brush;
        }

        private void SetSolidBackground(FrameworkElement? rootElement, FrameworkElement? titleBar)
        {
            var theme = rootElement?.ActualTheme ?? ElementTheme.Default;
            var bgColor = theme == ElementTheme.Dark
                ? Windows.UI.Color.FromArgb(255, 0, 0, 0)
                : Windows.UI.Color.FromArgb(255, 255, 255, 255);

            var brush = new SolidColorBrush(bgColor);
            if (rootElement is Microsoft.UI.Xaml.Controls.Panel rootPanel) rootPanel.Background = brush;
            if (titleBar is Microsoft.UI.Xaml.Controls.Panel titlePanel) titlePanel.Background = brush;
        }

        private void SetOverlayBackground(FrameworkElement? rootElement, FrameworkElement? titleBar)
        {
            try
            {
                var hex = _themeSettings.OverlayTintColor;
                if (!hex.StartsWith("#")) hex = "#" + hex;
                byte r = 0, g = 0, b = 0;
                if (hex.Length == 7)
                {
                    r = Convert.ToByte(hex.Substring(1, 2), 16);
                    g = Convert.ToByte(hex.Substring(3, 2), 16);
                    b = Convert.ToByte(hex.Substring(5, 2), 16);
                }

                var brush = new AcrylicBrush
                {
                    TintColor = ColorHelper.FromArgb(255, r, g, b),
                    TintOpacity = _themeSettings.OverlayTintOpacity,
                    TintLuminosityOpacity = _themeSettings.OverlayTintLuminosityOpacity,
                    TintTransitionDuration = TimeSpan.FromMilliseconds(_themeSettings.OverlayTintTransitionDurationMs),
                    FallbackColor = Colors.Transparent
                };

                if (rootElement is Microsoft.UI.Xaml.Controls.Panel rootPanel) rootPanel.Background = brush;
                if (titleBar is Microsoft.UI.Xaml.Controls.Panel titlePanel) titlePanel.Background = brush;
            }
            catch
            {
                SetTransparentBackground(rootElement, titleBar);
            }
        }
    }
}
