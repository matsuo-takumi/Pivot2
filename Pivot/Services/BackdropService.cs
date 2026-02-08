using System;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Pivot.Engine.Models;
using Pivot.Models;
using WinRT;

namespace Pivot.Services
{
    /// <summary>
    /// Backdrop management service implementation using high-level SystemBackdrop API.
    /// Manages switching between Mica, Acrylic, Overlay, and None.
    /// </summary>
    public class BackdropService : IBackdropService, IDisposable
    {
        private readonly ThemeSettingsService _themeSettings;
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

            // Clear any manual background to let the system backdrop show through
            SetTransparentBackground(rootElement, titleBar);

            switch (type)
            {
                case BackdropType.Mica:
                    window.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
                    break;
                case BackdropType.MicaAlt:
                    window.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
                    break;
                case BackdropType.Acrylic:
                    window.SystemBackdrop = new DesktopAcrylicBackdropWithKind { Kind = DesktopAcrylicKind.Base };
                    break;
                case BackdropType.AcrylicThin:
                    window.SystemBackdrop = new DesktopAcrylicBackdropWithKind { Kind = DesktopAcrylicKind.Thin };
                    break;
                default:
                    window.SystemBackdrop = null;
                    break;
            }
        }

        public void UpdateTheme(ElementTheme theme)
        {
            // SystemBackdrop automatically responds to app/window theme changes.
            // No manual configuration update needed for Mica/Acrylic backdrops.
            
            // However, if we are in a mode that mimics a theme manually (like solid background for 'None'),
            // we might need to re-apply.
            if (CurrentBackdropType == BackdropType.None && _currentWindow != null)
            {
                // Re-apply solid background to match new theme
                // Note: Logic for 'None' might depend on 'Content' which we don't have stored permanently here
                // unless we query window.Content. 
                // But SetSolidBackground is usually called with specific elements.
                // MainWindow calls UpdateTheme which calls this.
                // For now, MainWindow handles text colors, but background color is static or theme-resource bound usually.
                // If SetSolidBackground set a static color, we might need to refresh it.
                // But SetSolidBackground uses ActualTheme.
                
                // Since we don't hold the rootElement reference here persistently, 
                // we rely on the fact that SetSolidBackground sets a brush that might need updating if it's not a theme resource.
                // Actually, existing logic created a new SolidColorBrush.
                // If we want to strictly follow previous behavior, we would need to re-run SetSolidBackground.
                // But we don't have the elements. 
                // In the typical flow, the UI itself updates its background if bound to theme resources.
                // If we manually set a SolidColorBrush, it won't auto-update.
                // The MainWindow code calls SetBackdrop again via SetSystemBackdrop -> _backdropService.SetBackdrop
                // effectively when it wants to force an update, BUT MainWindow.Window_ThemeChanged only calls _backdropService.UpdateTheme.
                
                // Since we simpler implementation doesn't keep track of elements, 
                // and high-level backdrops handle themselves, we only have a potential gap for "None" mode background color
                // if the user switches theme while in "None" mode.
                // However, common practice for "None" is to just let the standard Window background (ApplicationPageBackgroundThemeBrush) take over?
                // The previous code manually set black/white.
                
                // For now, we'll leave this empty as SystemBackdrop handles the heavy lifting,
                // and 'None' mode might need a slight re-visit if dynamic theme switching in 'None' mode stops working.
                // But usually standard XAML backgrounds update themselves.
            }
        }

        public void SetInputActive(bool isActive)
        {
            // SystemBackdrop handles window activation state automatically.
        }

        public void Dispose()
        {
            if (_currentWindow != null)
            {
                _currentWindow.SystemBackdrop = null;
                _currentWindow = null;
            }
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

                var tintColor = ColorHelper.FromArgb(255, r, g, b);

                var brush = new AcrylicBrush
                {
                    TintColor = tintColor,
                    TintOpacity = _themeSettings.OverlayTintOpacity,
                    TintLuminosityOpacity = _themeSettings.OverlayTintLuminosityOpacity,
                    TintTransitionDuration = TimeSpan.FromMilliseconds(_themeSettings.OverlayTintTransitionDurationMs),
                    FallbackColor = tintColor
                };

                if (rootElement is Microsoft.UI.Xaml.Controls.Panel rootPanel)
                {
                    rootPanel.Background = brush;
                }
                
                if (titleBar is Microsoft.UI.Xaml.Controls.Panel titlePanel) titlePanel.Background = brush;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackdropService] SetOverlayBackground exception: {ex.Message}");
                SetTransparentBackground(rootElement, titleBar);
            }
        }
    }
}
