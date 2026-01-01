using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Pivot.Models;
using Pivot.Services;
using Pivot.Utilities;
using System;
using Windows.Foundation;

namespace Pivot.Controls
{
    public sealed class VulkanSwapChainPanel : SwapChainPanel
    {
        private VulkanInteropRenderer? _renderer;
        private bool _initialized = false;
        private readonly OrbitCamera _camera = new();
        
        // Mouse input tracking
        private Point _lastPointerPosition;
        private bool _isLeftDragging;
        private bool _isMiddleDragging;
        private bool _isRightDragging;
        
        // Gesture configuration
        private CameraGestureConfig _gestureConfig = CameraGestureConfig.FromPreset(CameraGesturePreset.Maya);

        /// <summary>
        /// Exposes the renderer for external model loading
        /// </summary>
        public VulkanInteropRenderer? Renderer => _renderer;
        
        /// <summary>
        /// Exposes the camera for external control
        /// </summary>
        public OrbitCamera Camera => _camera;

        public VulkanSwapChainPanel()
        {
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
            this.SizeChanged += OnSizeChanged;
            this.CompositionScaleChanged += OnCompositionScaleChanged;
            
            // Pointer events for camera control
            this.PointerPressed += OnPointerPressed;
            this.PointerMoved += OnPointerMoved;
            this.PointerReleased += OnPointerReleased;
            this.PointerWheelChanged += OnPointerWheelChanged;
            
            // Load gesture preset from settings
            LoadGesturePreset();
        }

        private void LoadGesturePreset()
        {
            try
            {
                var viewportSettings = App.Current?.Services?.GetService<ViewportSettingsService>();
                if (viewportSettings != null)
                {
                    var preset = viewportSettings.GetCameraGesture();
                    _gestureConfig = CameraGestureConfig.FromPreset(preset);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadGesturePreset error: {ex.Message}");
            }
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Refresh gesture config in case settings changed
            LoadGesturePreset();
            
            var point = e.GetCurrentPoint(this);
            var props = point.Properties;
            _lastPointerPosition = point.Position;
            
            // Check modifier keys
            var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
            var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
            var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
            
            bool isCtrl = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            bool isShift = (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            bool isAlt = (altState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            
            if (props.IsLeftButtonPressed)
            {
                _isLeftDragging = true;
                CapturePointer(e.Pointer);
            }
            else if (props.IsMiddleButtonPressed)
            {
                _isMiddleDragging = true;
                CapturePointer(e.Pointer);
            }
            else if (props.IsRightButtonPressed)
            {
                _isRightDragging = true;
                CapturePointer(e.Pointer);
            }
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var currentPos = e.GetCurrentPoint(this).Position;
            var deltaX = (float)(currentPos.X - _lastPointerPosition.X);
            var deltaY = (float)(currentPos.Y - _lastPointerPosition.Y);
            
            if (deltaX == 0 && deltaY == 0) 
            {
                return;
            }
            
            // Check modifier keys
            var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
            var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
            var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
            
            bool isCtrl = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            bool isShift = (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            bool isAlt = (altState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            
            // Determine action based on gesture config
            var action = GetCameraAction(isAlt, isShift, isCtrl);
            
            switch (action)
            {
                case CameraAction.Rotate:
                    _camera.Rotate(-deltaX * 0.01f, deltaY * 0.01f);
                    break;
                case CameraAction.Pan:
                    _camera.Pan(-deltaX, deltaY);
                    break;
                case CameraAction.Zoom:
                    _camera.Zoom(-deltaY * 0.05f);
                    break;
            }
            
            _lastPointerPosition = currentPos;
        }

        private CameraAction GetCameraAction(bool isAlt, bool isShift, bool isCtrl)
        {
            var cfg = _gestureConfig;
            
            // Check Rotate
            if (IsButtonPressed(cfg.RotateButton) &&
                cfg.RotateRequiresAlt == isAlt &&
                cfg.RotateRequiresShift == isShift &&
                cfg.RotateRequiresCtrl == isCtrl)
            {
                return CameraAction.Rotate;
            }
            
            // Check Pan
            if (IsButtonPressed(cfg.PanButton) &&
                cfg.PanRequiresAlt == isAlt &&
                cfg.PanRequiresShift == isShift &&
                cfg.PanRequiresCtrl == isCtrl)
            {
                return CameraAction.Pan;
            }
            
            // Check Zoom
            if (IsButtonPressed(cfg.ZoomButton) &&
                cfg.ZoomRequiresAlt == isAlt &&
                cfg.ZoomRequiresShift == isShift &&
                cfg.ZoomRequiresCtrl == isCtrl)
            {
                return CameraAction.Zoom;
            }
            
            return CameraAction.None;
        }

        private bool IsButtonPressed(MouseButton button)
        {
            return button switch
            {
                MouseButton.Left => _isLeftDragging,
                MouseButton.Middle => _isMiddleDragging,
                MouseButton.Right => _isRightDragging,
                _ => false
            };
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isLeftDragging = false;
            _isMiddleDragging = false;
            _isRightDragging = false;
            ReleasePointerCapture(e.Pointer);
        }

        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
            _camera.Zoom(delta / 120f);
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
            if (_initialized) return;
            
            try
            {
                // Calculate actual pixel size
                var scale = this.CompositionScaleX;
                if (scale <= 0) scale = 1;
                var width = (int)(this.ActualWidth * scale);
                var height = (int)(this.ActualHeight * scale);

                if (width <= 0 || height <= 0)
                {
                    // Size not ready yet, will try again on SizeChanged
                    return;
                }
                
                _renderer = new VulkanInteropRenderer();
                _renderer.Initialize(this, width, height);
                _initialized = true;
                
                System.Diagnostics.Debug.WriteLine($"[VulkanSwapChainPanel] Initialized: {width}x{height}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Renderer Initialization Failed: {ex}");
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Try to initialize if not yet done (size may have been 0 on Loaded)
            if (!_initialized)
            {
                InitializeRenderer();
            }
            
            // Resize if initialized
            if (_initialized && _renderer != null)
            {
                var scale = this.CompositionScaleX;
                if (scale <= 0) scale = 1;
                var width = (int)(e.NewSize.Width * scale);
                var height = (int)(e.NewSize.Height * scale);

                if (width > 0 && height > 0)
                {
                    _renderer.Resize(width, height);
                    System.Diagnostics.Debug.WriteLine($"[VulkanSwapChainPanel] Resized: {width}x{height}");
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
                _renderer.Render(_camera);
            }
        }

        private void Cleanup()
        {
            _renderer?.Dispose();
            _renderer = null;
            _initialized = false;
        }
        
        private enum CameraAction
        {
            None,
            Rotate,
            Pan,
            Zoom
        }
    }
}

