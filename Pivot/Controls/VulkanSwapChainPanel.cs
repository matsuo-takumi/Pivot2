using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pivot.Utilities;
using System;

namespace Pivot.Controls
{
    public sealed class VulkanSwapChainPanel : SwapChainPanel
    {
        private VulkanInteropRenderer? _renderer;
        private bool _initialized = false;

        public VulkanSwapChainPanel()
        {
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
            this.SizeChanged += OnSizeChanged;
            this.CompositionScaleChanged += OnCompositionScaleChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_initialized)
            {
                InitializeRenderer();
            }
            
            // Start Render Loop
            CompositionTarget.Rendering += OnRendering;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
            Cleanup();
        }

        private void InitializeRenderer()
        {
            try
            {
                _renderer = new VulkanInteropRenderer();
                
                // Calculate actual pixel size
                var scale = this.CompositionScaleX;
                var width = (int)(this.ActualWidth * scale);
                var height = (int)(this.ActualHeight * scale);

                if (width > 0 && height > 0)
                {
                    _renderer.Initialize(this, width, height);
                    _initialized = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Renderer Initialization Failed: {ex}");
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_initialized && _renderer != null)
            {
                var scale = this.CompositionScaleX;
                var width = (int)(e.NewSize.Width * scale);
                var height = (int)(e.NewSize.Height * scale);

                if (width > 0 && height > 0)
                {
                    _renderer.Resize(width, height);
                }
            }
        }

        private void OnCompositionScaleChanged(SwapChainPanel sender, object args)
        {
            if (_initialized && _renderer != null)
            {
                var scale = this.CompositionScaleX;
                var width = (int)(this.ActualWidth * scale);
                var height = (int)(this.ActualHeight * scale);

                if (width > 0 && height > 0)
                {
                    _renderer.Resize(width, height);
                }
            }
        }

        private void OnRendering(object? sender, object e)
        {
            if (_initialized && _renderer != null)
            {
                _renderer.Render();
            }
        }

        private void Cleanup()
        {
            _renderer?.Dispose();
            _renderer = null;
            _initialized = false;
        }
    }
}
