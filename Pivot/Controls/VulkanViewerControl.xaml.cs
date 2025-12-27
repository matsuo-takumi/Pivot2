using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Pivot.Utilities;
using Windows.Foundation;

namespace Pivot.Controls
{
    public sealed partial class VulkanViewerControl : UserControl
    {
        private VulkanInteropRenderer? _renderer;
        private bool _isInitialized;
        private bool _isDisposed;
        
        // Camera
        private readonly OrbitCamera _camera = new();
        private Point _lastPointerPosition;
        private bool _isLeftDragging;
        private bool _isMiddleDragging;
        
        // FPS tracking
        private readonly Stopwatch _fpsStopwatch = new();
        private int _frameCount;
        private double _lastFpsUpdate;
        
        public VulkanViewerControl()
        {
            this.InitializeComponent();
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
            this.SizeChanged += OnSizeChanged;
            
            // Wire up pointer events
            VulkanSwapChainPanel.PointerPressed += OnPointerPressed;
            VulkanSwapChainPanel.PointerMoved += OnPointerMoved;
            VulkanSwapChainPanel.PointerReleased += OnPointerReleased;
            VulkanSwapChainPanel.PointerWheelChanged += OnPointerWheelChanged;
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var props = e.GetCurrentPoint(VulkanSwapChainPanel).Properties;
            _lastPointerPosition = e.GetCurrentPoint(VulkanSwapChainPanel).Position;
            
            if (props.IsLeftButtonPressed)
            {
                _isLeftDragging = true;
                VulkanSwapChainPanel.CapturePointer(e.Pointer);
            }
            else if (props.IsMiddleButtonPressed)
            {
                _isMiddleDragging = true;
                VulkanSwapChainPanel.CapturePointer(e.Pointer);
            }
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var currentPos = e.GetCurrentPoint(VulkanSwapChainPanel).Position;
            var deltaX = (float)(currentPos.X - _lastPointerPosition.X);
            var deltaY = (float)(currentPos.Y - _lastPointerPosition.Y);
            
            if (_isLeftDragging)
            {
                // Rotate camera
                _camera.Rotate(-deltaX * 0.01f, deltaY * 0.01f);
            }
            else if (_isMiddleDragging)
            {
                // Pan camera
                _camera.Pan(-deltaX, deltaY);
            }
            
            _lastPointerPosition = currentPos;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isLeftDragging = false;
            _isMiddleDragging = false;
            VulkanSwapChainPanel.ReleasePointerCapture(e.Pointer);
        }

        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(VulkanSwapChainPanel).Properties.MouseWheelDelta;
            _camera.Zoom(delta / 120f);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InitializeRenderer();
            StartRenderLoop();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopRenderLoop();
            DisposeRenderer();
        }

        private void InitializeRenderer()
        {
            if (_isInitialized) return;

            try
            {
                var width = (int)Math.Max(1, VulkanSwapChainPanel.ActualWidth);
                var height = (int)Math.Max(1, VulkanSwapChainPanel.ActualHeight);

                UpdateDebugStatus("Creating renderer...");
                
                _renderer = new VulkanInteropRenderer();
                _renderer.Initialize(VulkanSwapChainPanel, width, height);
                
                _isInitialized = true;
                _fpsStopwatch.Start();
                
                UpdateDebugStatus("Initialized");
                UpdateDebugSize(width, height);
                
                Debug.WriteLine($"[VulkanViewerControl] Initialized with size {width}x{height}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VulkanViewerControl] Initialization failed: {ex}");
                ShowError($"Vulkan initialization failed:\n{ex.Message}");
            }
        }

        private void DisposeRenderer()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            
            _fpsStopwatch.Stop();
            
            try
            {
                _renderer?.Dispose();
                _renderer = null;
                _isInitialized = false;
                Debug.WriteLine("[VulkanViewerControl] Disposed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VulkanViewerControl] Dispose error: {ex}");
            }
        }

        private void StartRenderLoop()
        {
            CompositionTarget.Rendering += OnRendering;
            Debug.WriteLine("[VulkanViewerControl] Render loop started");
        }

        private void StopRenderLoop()
        {
            CompositionTarget.Rendering -= OnRendering;
            Debug.WriteLine("[VulkanViewerControl] Render loop stopped");
        }

        private void OnRendering(object? sender, object e)
        {
            if (!_isInitialized || _isDisposed || _renderer == null) return;

            try
            {
                _renderer.Render(_camera);
                
                // Update FPS counter
                _frameCount++;
                var elapsed = _fpsStopwatch.Elapsed.TotalSeconds;
                if (elapsed - _lastFpsUpdate >= 1.0)
                {
                    double timeDiff = elapsed - _lastFpsUpdate;
                    var fps = timeDiff > 0.001 ? _frameCount / timeDiff : 0;
                    UpdateDebugFps(fps);
                    _frameCount = 0;
                    _lastFpsUpdate = elapsed;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VulkanViewerControl] Render error: {ex}");
                StopRenderLoop();
                ShowError($"Render error:\n{ex.Message}");
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isInitialized || _renderer == null) return;

            var width = (int)Math.Max(1, e.NewSize.Width);
            var height = (int)Math.Max(1, e.NewSize.Height);

            try
            {
                _renderer.Resize(width, height);
                UpdateDebugSize(width, height);
                Debug.WriteLine($"[VulkanViewerControl] Resized to {width}x{height}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VulkanViewerControl] Resize error: {ex}");
            }
        }

        private void UpdateDebugFps(double fps)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                DebugFpsText.Text = $"FPS: {fps:F1}";
            });
        }

        private void UpdateDebugSize(int width, int height)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                DebugSizeText.Text = $"Size: {width} x {height}";
            });
        }

        private void UpdateDebugStatus(string status)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                DebugStatusText.Text = $"Status: {status}";
            });
        }

        private void ShowError(string message)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ErrorText.Text = message;
                ErrorOverlay.Visibility = Visibility.Visible;
                DebugOverlay.Visibility = Visibility.Collapsed;
            });
        }
    }
}
