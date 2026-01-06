using System;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinRT;

namespace Pivot.Services
{
    /// <summary>
    /// Custom SystemBackdrop that wraps DesktopAcrylicController with Kind support.
    /// Enables AcrylicThin variant which is not available in the standard DesktopAcrylicBackdrop.
    /// </summary>
    public class DesktopAcrylicBackdropWithKind : SystemBackdrop
    {
        private DesktopAcrylicController? _controller;
        private SystemBackdropConfiguration? _configurationSource;
        private FrameworkElement? _rootElement;

        /// <summary>
        /// Gets or sets the Acrylic variant (Base or Thin).
        /// </summary>
        public DesktopAcrylicKind Kind { get; set; } = DesktopAcrylicKind.Base;

        protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
        {
            base.OnTargetConnected(connectedTarget, xamlRoot);

            if (!DesktopAcrylicController.IsSupported())
            {
                return;
            }

            // Create controller with the specified Kind
            _controller = new DesktopAcrylicController { Kind = this.Kind };

            // Set up configuration source for theme tracking
            _configurationSource = new SystemBackdropConfiguration();
            
            // Get the initial theme from XamlRoot and store reference for cleanup
            if (xamlRoot.Content is FrameworkElement rootElement)
            {
                _rootElement = rootElement;
                _configurationSource.Theme = ConvertToSystemBackdropTheme(rootElement.ActualTheme);
                _rootElement.ActualThemeChanged += OnActualThemeChanged;
            }
            
            _controller.SetSystemBackdropConfiguration(_configurationSource);
            _controller.AddSystemBackdropTarget(connectedTarget);
        }

        protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
        {
            base.OnTargetDisconnected(disconnectedTarget);

            // Unsubscribe from theme changes first
            if (_rootElement != null)
            {
                _rootElement.ActualThemeChanged -= OnActualThemeChanged;
                _rootElement = null;
            }

            _controller?.RemoveSystemBackdropTarget(disconnectedTarget);
            _controller?.Dispose();
            _controller = null;
            _configurationSource = null;
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            if (_configurationSource != null)
            {
                _configurationSource.Theme = ConvertToSystemBackdropTheme(sender.ActualTheme);
            }
        }

        /// <summary>
        /// Override to handle theme changes safely. Without this override, WinUI's base implementation
        /// throws ArgumentException ("The parameter is incorrect") during theme changes.
        /// </summary>
        protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
        {
            // Do NOT call base.OnDefaultSystemBackdropConfigurationChanged - it throws an error.
            // Instead, we handle theme changes ourselves through OnActualThemeChanged.
            // This is because we use DesktopAcrylicController directly instead of relying
            // on the default system backdrop configuration mechanism.
            
            // Update configuration based on current theme if needed
            if (_configurationSource != null && xamlRoot?.Content is FrameworkElement rootElement)
            {
                _configurationSource.Theme = ConvertToSystemBackdropTheme(rootElement.ActualTheme);
            }
        }

        private static SystemBackdropTheme ConvertToSystemBackdropTheme(ElementTheme theme)
        {
            return theme switch
            {
                ElementTheme.Dark => SystemBackdropTheme.Dark,
                ElementTheme.Light => SystemBackdropTheme.Light,
                _ => SystemBackdropTheme.Default
            };
        }
    }
}

