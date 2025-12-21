using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Pivot.Services;
using Pivot.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Pivot.Controls
{
    public sealed partial class ModelViewerControl : UserControl
    {
        private VulkanInteropRenderer? _renderer;
        private readonly OrbitCamera _camera = new();
        private bool _isInitialized;
        private bool _isDisposed;
        
        private BoundingBox _currentBounds;
        private bool _hasBounds = false;
        
        // Mouse input
        private Point _lastPointerPosition;
        private bool _isLeftDragging;
        private bool _isMiddleDragging;
        private bool _isRightDragging;
        
        // FPS tracking
        private readonly Stopwatch _fpsStopwatch = new();
        private int _frameCount;
        private double _lastFpsUpdate;
        
        // Model stats
        private int _polygonCount;
        private int _vertexCount;
        private int _uvSetCount;
        private int _materialCount;
        
        // Settings
        private SettingsService? _settings;

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
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
            this.SizeChanged += OnSizeChanged;
            
            // Pointer events
            VulkanSwapChainPanel.PointerPressed += OnPointerPressed;
            VulkanSwapChainPanel.PointerMoved += OnPointerMoved;
            VulkanSwapChainPanel.PointerReleased += OnPointerReleased;
            VulkanSwapChainPanel.PointerWheelChanged += OnPointerWheelChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.Focus(FocusState.Programmatic);
            
            // Get settings
            _settings = App.Current?.Services?.GetService<SettingsService>();
            
            InitializeRenderer();
            UpdateInfoVisibility();
            
            _fpsStopwatch.Start();
            CompositionTarget.Rendering += OnRendering;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
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
                _renderer.Render(_camera);
                AxisGizmoControl.UpdateFromCamera(_camera);
                
                // Update FPS counter
                _frameCount++;
                var elapsed = _fpsStopwatch.Elapsed.TotalSeconds;
                if (elapsed - _lastFpsUpdate >= 0.5) // Update every 0.5 sec
                {
                    var fps = _frameCount / (elapsed - _lastFpsUpdate);
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
                        CameraInfoText.Text = $"Camera: d={_camera.Distance:F2}";
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
            if (_settings == null) return;
            
            // Model Info
            PolygonCountText.Visibility = _settings.GetShowPolygonCount() ? Visibility.Visible : Visibility.Collapsed;
            VertexCountText.Visibility = _settings.GetShowVertexCount() ? Visibility.Visible : Visibility.Collapsed;
            UVSetCountText.Visibility = _settings.GetShowUVSetCount() ? Visibility.Visible : Visibility.Collapsed;
            MaterialCountText.Visibility = _settings.GetShowMaterialCount() ? Visibility.Visible : Visibility.Collapsed;
            BoundingBoxText.Visibility = _settings.GetShowBoundingBox() ? Visibility.Visible : Visibility.Collapsed;
            
            // Viewport Stats  
            FpsText.Visibility = _settings.GetShowFPS() ? Visibility.Visible : Visibility.Collapsed;
            ResolutionText.Visibility = _settings.GetShowResolution() ? Visibility.Visible : Visibility.Collapsed;
            ViewportSizeText.Visibility = _settings.GetShowViewportSize() ? Visibility.Visible : Visibility.Collapsed;
            CameraInfoText.Visibility = _settings.GetShowCameraInfo() ? Visibility.Visible : Visibility.Collapsed;
            
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

        private void UpdateModelStats()
        {
            PolygonCountText.Text = $"Polygons: {_polygonCount:N0}";
            VertexCountText.Text = $"Vertices: {_vertexCount:N0}";
            UVSetCountText.Text = $"UV Sets: {_uvSetCount}";
            MaterialCountText.Text = $"Materials: {_materialCount}";
            
            if (_hasBounds)
            {
                var size = _currentBounds.Size;
                BoundingBoxText.Text = $"Bounds: {size.X:F2} x {size.Y:F2} x {size.Z:F2}";
            }
        }

        #region Pointer Events
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

            // Alt+LMB = Rotate (Maya style default)
            var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
            bool isAlt = (altState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

            if (_isLeftDragging && isAlt)
            {
                _camera.Rotate(-deltaX * 0.01f, deltaY * 0.01f);
            }
            else if (_isMiddleDragging)
            {
                _camera.Pan(-deltaX, deltaY);
            }
            else if (_isRightDragging && isAlt)
            {
                _camera.Zoom(-deltaY * 0.1f);
            }

            _lastPointerPosition = currentPos;
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
            _camera.Zoom(delta / 120f);
        }
        #endregion

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.F)
            {
                FitCameraToModel();
                e.Handled = true;
            }
        }

        private void FitButton_Click(object sender, RoutedEventArgs e)
        {
            FitCameraToModel();
        }

        private void FitCameraToModel()
        {
            if (!_hasBounds) return;
            _camera.FitToBounds(_currentBounds);
        }

        private static void OnModelPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ModelViewerControl control && e.NewValue is string path)
            {
                control.LoadModelAsync(path);
            }
        }

        private async void LoadModelAsync(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                LoadingIndicator.Visibility = Visibility.Visible;
                ErrorMessageBorder.Visibility = Visibility.Collapsed;

                if (!_isInitialized || _renderer == null)
                {
                    ShowError("Renderer not initialized", "Please wait for initialization.");
                    return;
                }

                MeshData? meshData = null;
                await Task.Run(() =>
                {
                    using var loader = new ModelLoader();
                    meshData = loader.LoadModel(path);
                });

                if (meshData != null)
                {
                    _renderer.UploadMesh(meshData.Vertices, meshData.Indices);
                    
                    _currentBounds = meshData.Bounds;
                    _hasBounds = true;
                    
                    // Store model stats
                    _polygonCount = meshData.Indices.Length / 3;
                    _vertexCount = meshData.Vertices.Length;
                    _uvSetCount = meshData.UVSetCount;
                    _materialCount = meshData.MaterialCount;
                    
                    UpdateModelStats();
                    
                    _camera.FitToBounds(meshData.Bounds);
                }

                LoadingIndicator.Visibility = Visibility.Collapsed;
                Debug.WriteLine($"[ModelViewerControl] Loaded: {path}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModelViewerControl] Load error: {ex.Message}");
                ShowError("モデルの読み込みに失敗しました", ex.Message);
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


