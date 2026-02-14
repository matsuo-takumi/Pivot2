using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Pivot.Engine.Models;
using Pivot.Models;
using Pivot.Services;
using Pivot.Utilities;
using Pivot.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Messages;

namespace Pivot.Controls
{
    public sealed partial class ModelViewerControl : UserControl
    {
        private readonly ModelViewerViewModel _viewModel = new();
        private VulkanInteropRenderer? _renderer;
        private bool _isInitialized;
        private bool _isDisposed;
        
        // Mouse input
        private Point _lastPointerPosition;
        private bool _isLeftDragging;
        private bool _isMiddleDragging;
        private bool _isRightDragging;

        
        // FPS tracking
        private readonly Stopwatch _fpsStopwatch = new();
        private int _frameCount;
        private double _lastFpsUpdate;
        
        // Settings
        private ViewportSettingsService? _viewportSettings;
        private CameraGestureConfig _gestureConfig = CameraGestureConfig.FromPreset(CameraGesturePreset.Maya);

        public static readonly DependencyProperty ViewerBackgroundProperty =
            DependencyProperty.Register(nameof(ViewerBackground), typeof(Microsoft.UI.Xaml.Media.Brush),
                typeof(ModelViewerControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ModelPathProperty =
            DependencyProperty.Register(nameof(ModelPath), typeof(string),
                typeof(ModelViewerControl),
                new PropertyMetadata(null, OnModelPathChanged));

        public Microsoft.UI.Xaml.Media.Brush ViewerBackground
        {
            get => (Microsoft.UI.Xaml.Media.Brush)GetValue(ViewerBackgroundProperty);
            set => SetValue(ViewerBackgroundProperty, value);
        }

        public string? ModelPath
        {
            get => (string?)GetValue(ModelPathProperty);
            set => SetValue(ModelPathProperty, value);
        }

        public ModelViewerControl()
        {
            this.InitializeComponent();
            // Note: Do NOT set DataContext here to preserve parent binding context
            // Use x:Bind to access _viewModel directly instead

            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
            this.SizeChanged += OnSizeChanged;
            
            // Pointer events
            VulkanSwapChainPanel.PointerPressed += OnPointerPressed;
            VulkanSwapChainPanel.PointerMoved += OnPointerMoved;
            VulkanSwapChainPanel.PointerReleased += OnPointerReleased;
            VulkanSwapChainPanel.PointerWheelChanged += OnPointerWheelChanged;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.Focus(FocusState.Programmatic);
            
            // Get settings
            _viewportSettings = App.Current?.Services?.GetService<ViewportSettingsService>();
            
            // Load gesture preset
            if (_viewportSettings != null)
            {
                var preset = _viewportSettings.GetCameraGesture();
                _gestureConfig = CameraGestureConfig.FromPreset(preset);
            }
            
            // Pass ViewModel to LightingSettingsView
            FloatingLightingSettings.ViewModel = _viewModel;
            
            // Restore lighting state from settings
            await _viewModel.RestoreLightingStateAsync();
            
            UpdateInfoVisibility();
            
            // Defer Vulkan initialization to allow UI to render first
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                InitializeRenderer();
                
                // Apply backface culling setting
                if (_viewportSettings != null && _renderer != null)
                {
                    _renderer.SetBackfaceCulling(_viewportSettings.GetBackfaceCulling());

                }
                
                // Hide loading indicator after initialization
                LoadingIndicator.Visibility = Visibility.Collapsed;
                
                _fpsStopwatch.Start();
                CompositionTarget.Rendering += OnRendering;
            });

            // Register for settings changes
            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {

            });
        }

        private async void OnUnloaded(object sender, RoutedEventArgs e)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
            
            // Save lighting state before unloading
            await _viewModel.SaveLightingStateAsync();
            
            _fpsStopwatch.Stop();
            CompositionTarget.Rendering -= OnRendering;
            DisposeRenderer();
        }

        private void InitializeRenderer()
        {
            if (_isInitialized) return;

            try
            {
                var width = (int)Math.Max(1, VulkanSwapChainPanel.ActualWidth);
                var height = (int)Math.Max(1, VulkanSwapChainPanel.ActualHeight);

                if (width <= 0 || height <= 0) return;

                _renderer = new VulkanInteropRenderer();
                _renderer.Initialize(VulkanSwapChainPanel, width, height);
                
                // Pass renderer to ViewModel
                _viewModel.SetRenderer(_renderer);

                _isInitialized = true;
                Debug.WriteLine($"[ModelViewerControl] Initialized: {width}x{height}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModelViewerControl] Init failed: {ex}");
                ShowError("Vulkan initialization failed", ex.Message);
            }
        }

        private void DisposeRenderer()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                _viewModel.SetRenderer(null);
                _renderer?.Dispose();
                _renderer = null;
                _isInitialized = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModelViewerControl] Dispose error: {ex}");
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isInitialized)
            {
                InitializeRenderer();
            }
            
            if (_isInitialized && _renderer != null)
            {
                var width = (int)Math.Max(1, e.NewSize.Width);
                var height = (int)Math.Max(1, e.NewSize.Height);

                if (width > 0 && height > 0)
                {
                    try
                    {
                        _renderer.Resize(width, height);
                        Debug.WriteLine($"[ModelViewerControl] Resized: {width}x{height}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ModelViewerControl] Resize error: {ex}");
                    }
                }
            }
        }

        private void OnRendering(object? sender, object e)
        {
            if (!_isInitialized || _isDisposed || _renderer == null) return;

            try
            {
                _renderer.Render(_viewModel.Camera);
                AxisGizmoControl.UpdateFromCamera(_viewModel.Camera);
                
                // Update FPS counter
                _frameCount++;
                var elapsed = _fpsStopwatch.Elapsed.TotalSeconds;
                if (elapsed - _lastFpsUpdate >= 0.5) // Update every 0.5 sec
                {
                    double timeDiff = elapsed - _lastFpsUpdate;
                    var fps = timeDiff > 0.001 ? _frameCount / timeDiff : 0;

                    FpsText.Text = $"FPS: {fps:F0}";
                    _frameCount = 0;
                    _lastFpsUpdate = elapsed;
                    
                    // Update viewport info
                    if (ResolutionText.Visibility == Visibility.Visible)
                    {
                        var scale = VulkanSwapChainPanel.CompositionScaleX;
                        var rW = (int)(VulkanSwapChainPanel.ActualWidth * scale);
                        var rH = (int)(VulkanSwapChainPanel.ActualHeight * scale);
                        ResolutionText.Text = $"Resolution: {rW} x {rH}";
                    }
                    
                    if (ViewportSizeText.Visibility == Visibility.Visible)
                    {
                        ViewportSizeText.Text = $"Viewport: {VulkanSwapChainPanel.ActualWidth:F0} x {VulkanSwapChainPanel.ActualHeight:F0}";
                    }
                    
                    if (CameraInfoText.Visibility == Visibility.Visible)
                    {
                        CameraInfoText.Text = $"Camera: d={_viewModel.Camera.Distance:F2}";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModelViewerControl] Render error: {ex}");
            }
        }

        private void UpdateInfoVisibility()
        {
            if (_viewportSettings == null) return;
            
            // Model Info
            PolygonCountText.Visibility = _viewportSettings.GetShowPolygonCount() ? Visibility.Visible : Visibility.Collapsed;
            VertexCountText.Visibility = _viewportSettings.GetShowVertexCount() ? Visibility.Visible : Visibility.Collapsed;
            UVSetCountText.Visibility = _viewportSettings.GetShowUVSetCount() ? Visibility.Visible : Visibility.Collapsed;
            MaterialCountText.Visibility = _viewportSettings.GetShowMaterialCount() ? Visibility.Visible : Visibility.Collapsed;
            BoundingBoxText.Visibility = _viewportSettings.GetShowBoundingBox() ? Visibility.Visible : Visibility.Collapsed;
            
            // Viewport Stats  
            FpsText.Visibility = _viewportSettings.GetShowFPS() ? Visibility.Visible : Visibility.Collapsed;
            ResolutionText.Visibility = _viewportSettings.GetShowResolution() ? Visibility.Visible : Visibility.Collapsed;
            ViewportSizeText.Visibility = _viewportSettings.GetShowViewportSize() ? Visibility.Visible : Visibility.Collapsed;
            CameraInfoText.Visibility = _viewportSettings.GetShowCameraInfo() ? Visibility.Visible : Visibility.Collapsed;
            
            // Show separator if both sections have visible items
            bool hasModelStats = PolygonCountText.Visibility == Visibility.Visible || 
                                 VertexCountText.Visibility == Visibility.Visible;
            bool hasViewportStats = FpsText.Visibility == Visibility.Visible || 
                                    ResolutionText.Visibility == Visibility.Visible;
            InfoSeparator.Visibility = hasModelStats && hasViewportStats ? Visibility.Visible : Visibility.Collapsed;
            
            // Hide entire overlay if nothing is visible
            bool anyVisible = hasModelStats || hasViewportStats || 
                              CameraInfoText.Visibility == Visibility.Visible ||
                              ViewportSizeText.Visibility == Visibility.Visible;
            InfoOverlay.Visibility = anyVisible ? Visibility.Visible : Visibility.Collapsed;
        }



        #region Pointer Events
        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Capture focus so keyboard shortcuts work
            this.Focus(FocusState.Programmatic);
            
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
            else if (props.IsRightButtonPressed)
            {
                _isRightDragging = true;
                VulkanSwapChainPanel.CapturePointer(e.Pointer);
            }
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var currentPos = e.GetCurrentPoint(VulkanSwapChainPanel).Position;
            var deltaX = (float)(currentPos.X - _lastPointerPosition.X);
            var deltaY = (float)(currentPos.Y - _lastPointerPosition.Y);

            // Get modifier key states
            var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
            var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
            var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
            var lKeyState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.L);
            bool isAlt = (altState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            bool isShift = (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            bool isCtrl = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            bool isL = (lKeyState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

            // Ctrl+L light rotation (UE-style)
            if (isCtrl && isL && _isLeftDragging)
            {
                _viewModel.RotateLight(deltaX * 0.01f, deltaY * 0.01f);
            }
            // Check Rotate gesture
            else if (CheckGesture(_gestureConfig.RotateButton, _gestureConfig.RotateRequiresAlt, _gestureConfig.RotateRequiresShift, _gestureConfig.RotateRequiresCtrl, isAlt, isShift, isCtrl))
            {
                _viewModel.RotateCamera((-deltaX * 0.01f, deltaY * 0.01f));
            }
            // Check Pan gesture
            else if (CheckGesture(_gestureConfig.PanButton, _gestureConfig.PanRequiresAlt, _gestureConfig.PanRequiresShift, _gestureConfig.PanRequiresCtrl, isAlt, isShift, isCtrl))
            {
                _viewModel.PanCamera((-deltaX, deltaY));
            }
            // Check Zoom gesture
            else if (CheckGesture(_gestureConfig.ZoomButton, _gestureConfig.ZoomRequiresAlt, _gestureConfig.ZoomRequiresShift, _gestureConfig.ZoomRequiresCtrl, isAlt, isShift, isCtrl))
            {
                _viewModel.ZoomCamera(-deltaY * 0.1f);
            }

            _lastPointerPosition = currentPos;
        }

        private bool CheckGesture(MouseButton button, bool requiresAlt, bool requiresShift, bool requiresCtrl, bool isAlt, bool isShift, bool isCtrl)
        {
            // Check button
            bool buttonPressed = button switch
            {
                MouseButton.Left => _isLeftDragging,
                MouseButton.Middle => _isMiddleDragging,
                MouseButton.Right => _isRightDragging,
                _ => false
            };
            if (!buttonPressed) return false;
            
            // Check modifiers
            if (requiresAlt && !isAlt) return false;
            if (requiresShift && !isShift) return false;
            if (requiresCtrl && !isCtrl) return false;
            
            return true;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isLeftDragging = false;
            _isMiddleDragging = false;
            _isRightDragging = false;
            VulkanSwapChainPanel.ReleasePointerCapture(e.Pointer);
        }

        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(VulkanSwapChainPanel).Properties.MouseWheelDelta;
            _viewModel.ZoomCamera(delta / 120f);
        }
        #endregion

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.F)
            {
                _viewModel.ResetCamera();
                e.Handled = true;
                return;
            }
            
            // Check Alt modifier for shading mode shortcuts
            var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
            bool isAlt = (altState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            
            if (isAlt)
            {
                switch (e.Key)
                {
                    case Windows.System.VirtualKey.Number1:
                        _viewModel.SetShadingMode(ShadingMode.Lit);
                        e.Handled = true;
                        break;
                    case Windows.System.VirtualKey.Number2:
                        _viewModel.SetShadingMode(ShadingMode.Depth);
                        e.Handled = true;
                        break;
                    case Windows.System.VirtualKey.Number3:
                        _viewModel.SetShadingMode(ShadingMode.WorldNormal);
                        e.Handled = true;
                        break;
                    case Windows.System.VirtualKey.Number4:
                        _viewModel.SetShadingMode(ShadingMode.Combined);
                        e.Handled = true;
                        break;
                }
            }
        }



        private static void OnModelPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ModelViewerControl control && e.NewValue is string path)
            {
                control._viewModel.ModelPath = path;
            }
        }

        private void ShowError(string title, string detail)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                ErrorMessageText.Text = title;
                ErrorDetailText.Text = detail;
                ErrorDetailText.Visibility = string.IsNullOrWhiteSpace(detail) ? Visibility.Collapsed : Visibility.Visible;
                ErrorMessageBorder.Visibility = Visibility.Visible;
            });
        }
    }
}


