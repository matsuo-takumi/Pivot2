using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Pivot.Models;
using Pivot.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Pivot.ViewModels
{
    public partial class ModelViewerViewModel : ObservableObject
    {
        private readonly Pivot.Utilities.OrbitCamera _camera = new();
        private Pivot.Utilities.VulkanInteropRenderer? _renderer;
        private Pivot.Utilities.BoundingBox _currentBounds;
        private SettingsService? _settingsService;
        private ViewportBackgroundMode _backgroundMode = ViewportBackgroundMode.Custom;

        public ModelViewerViewModel()
        {
            _settingsService = App.Current?.Services?.GetService<SettingsService>();
            var messenger = App.Current?.Services?.GetService<IMessenger>();
            
            if (messenger != null)
            {
                messenger.Register<ModelViewerViewModel, Pivot.Messages.SettingsChangedMessage>(this, static (r, m) => r.OnSettingsChanged(m));
                messenger.Register<ModelViewerViewModel, Pivot.Messages.ThemeChangedMessage>(this, static (r, m) => r.OnThemeChanged(m));
            }
        }
        
        private void OnSettingsChanged(Pivot.Messages.SettingsChangedMessage message)
        {
            if (message.Value == "Viewport.BackgroundMode" || message.Value == "Viewport.BackgroundColor")
            {
                // UIスレッドで実行する必要があるかもしれないが、プロパティ変更は通常マーシャリングされる
                //念のためDispatcherQueueを使うのが安全だが、ここでは直接呼び出してみる
                InitializeBackgroundFromSettings();
            }
            else if (message.Value == "MaterialParams")
            {
                ApplyMaterialParams();
            }
        }

        private void OnThemeChanged(Pivot.Messages.ThemeChangedMessage message)
        {
            // テーマ変更時はMatchThemeの場合のみ再適用
            if (_backgroundMode == ViewportBackgroundMode.MatchTheme)
            {
                ApplyThemeBasedBackground();
            }
        }
        
        [ObservableProperty]
        private string? _modelPath;
        
        [ObservableProperty]
        private bool _isLoading;
        
        [ObservableProperty]
        private string? _errorMessage;
        
        [ObservableProperty]
        private bool _hasError;
        
        public Pivot.Utilities.OrbitCamera Camera => _camera;
        
        /// <summary>
        /// Set the renderer instance (called when VulkanSwapChainPanel initializes)
        /// </summary>
        public void SetRenderer(Pivot.Utilities.VulkanInteropRenderer? renderer)
        {
            _renderer = renderer;
            
            // Initialize background from settings
            InitializeBackgroundFromSettings();
            
            // If we have a pending model path, load it now
            if (_renderer != null && !string.IsNullOrWhiteSpace(ModelPath) && File.Exists(ModelPath))
            {
                _ = LoadModelAsync(ModelPath);
            }
        }
        
        /// <summary>
        /// Initialize background color from settings
        /// </summary>
        private void InitializeBackgroundFromSettings()
        {
            try
            {
                _settingsService ??= App.Current?.Services?.GetService<SettingsService>();
                if (_settingsService == null) return;
                
                _backgroundMode = _settingsService.GetViewportBackgroundMode();
                
                if (_backgroundMode == ViewportBackgroundMode.MatchTheme)
                {
                    ApplyThemeBasedBackground();
                }
                else
                {
                    var hexColor = _settingsService.GetViewportBackgroundColor();
                    BackgroundColor = HexToColor(hexColor);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelViewerViewModel] InitializeBackgroundFromSettings error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Apply background color based on current Mica backdrop style and theme
        /// </summary>
        public void ApplyThemeBasedBackground()
        {
            try
            {
                _settingsService ??= App.Current?.Services?.GetService<SettingsService>();
                _backgroundMode = _settingsService?.GetViewportBackgroundMode() ?? ViewportBackgroundMode.Custom;
                
                if (_backgroundMode != ViewportBackgroundMode.MatchTheme)
                {
                    // Use custom color from settings
                    var hexColor = _settingsService?.GetViewportBackgroundColor() ?? "#3399CC";
                    BackgroundColor = HexToColor(hexColor);
                    return;
                }
                
                // MatchTheme mode: Set transparent background to show Mica backdrop
                _renderer?.SetTransparentBackground();
                
                // Also update BackgroundColor property with a transparent value for consistency
                BackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                
                System.Diagnostics.Debug.WriteLine("[ModelViewerViewModel] Applied transparent background for Mica");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelViewerViewModel] ApplyThemeBasedBackground error: {ex.Message}");
            }
        }

        
        private static Windows.UI.Color HexToColor(string hex)
        {
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length == 6)
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return Windows.UI.Color.FromArgb(255, r, g, b);
                }
            }
            catch { }
            return Windows.UI.Color.FromArgb(255, 51, 153, 204); // Default
        }
        
        /// <summary>
        /// Apply PBR material parameters from settings
        /// </summary>
        private void ApplyMaterialParams()
        {
            try
            {
                _settingsService ??= App.Current?.Services?.GetService<SettingsService>();
                if (_settingsService == null || _renderer == null) return;
                
                var (r, g, b, metallic, roughness) = _settingsService.GetMaterialParams();
                _renderer.SetMaterialParams(r, g, b, metallic, roughness);
                
                System.Diagnostics.Debug.WriteLine($"[ModelViewerViewModel] Applied material params: RGB({r:F2},{g:F2},{b:F2}) M={metallic:F2} R={roughness:F2}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelViewerViewModel] ApplyMaterialParams error: {ex.Message}");
            }
        }

        
        [RelayCommand]
        public void ResetCamera()
        {
            _camera.FitToBounds(_currentBounds);
        }
        
        [RelayCommand]
        public void RotateCamera(object? param)
        {
            if (param is (float deltaYaw, float deltaPitch))
            {
                _camera.Rotate(deltaYaw, deltaPitch);
            }
        }
        
        [RelayCommand]
        public void PanCamera(object? param)
        {
            if (param is (float deltaX, float deltaY))
            {
                _camera.Pan(deltaX, deltaY);
            }
        }
        
        [ObservableProperty]
        private Pivot.Models.ShadingMode _currentShadingMode = Pivot.Models.ShadingMode.WorldNormal;

        partial void OnCurrentShadingModeChanged(Pivot.Models.ShadingMode value)
        {
            _renderer?.SetShadingMode(value);
        }

        [RelayCommand]
        public void SetShadingMode(Pivot.Models.ShadingMode mode)
        {
            CurrentShadingMode = mode;
        }

        // Lighting and Background
        [ObservableProperty]
        private Windows.UI.Color _backgroundColor = Windows.UI.Color.FromArgb(255, 51, 153, 204);

        [ObservableProperty]
        private System.Numerics.Vector3 _lightDirection = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(0.5f, 1.0f, 0.3f));

        [ObservableProperty]
        private float _lightIntensity = 1.0f;

        [ObservableProperty]
        private Windows.UI.Color _lightColor = Windows.UI.Color.FromArgb(255, 255, 255, 255); // White

        // Spherical coordinates for light (yaw/pitch in radians)
        private float _lightYaw = 0.3f;
        private float _lightPitch = 1.0f;

        partial void OnBackgroundColorChanged(Windows.UI.Color value)
        {
            _renderer?.SetBackgroundColor(value.R / 255f, value.G / 255f, value.B / 255f, value.A / 255f);
        }

        partial void OnLightDirectionChanged(System.Numerics.Vector3 value)
        {
            _renderer?.SetLightParams(value, LightIntensity, new System.Numerics.Vector3(LightColor.R / 255f, LightColor.G / 255f, LightColor.B / 255f));
        }

        partial void OnLightIntensityChanged(float value)
        {
            _renderer?.SetLightParams(LightDirection, value, new System.Numerics.Vector3(LightColor.R / 255f, LightColor.G / 255f, LightColor.B / 255f));
        }

        partial void OnLightColorChanged(Windows.UI.Color value)
        {
            _renderer?.SetLightParams(LightDirection, LightIntensity, new System.Numerics.Vector3(value.R / 255f, value.G / 255f, value.B / 255f));
        }

        /// <summary>
        /// Rotate the light direction by yaw/pitch deltas (called during Ctrl+L drag)
        /// </summary>
        public void RotateLight(float deltaYaw, float deltaPitch)
        {
            _lightYaw += deltaYaw;
            _lightPitch = Math.Clamp(_lightPitch + deltaPitch, 0.1f, (float)Math.PI - 0.1f);

            // Convert spherical to cartesian
            float x = (float)(Math.Sin(_lightPitch) * Math.Cos(_lightYaw));
            float y = (float)Math.Cos(_lightPitch);
            float z = (float)(Math.Sin(_lightPitch) * Math.Sin(_lightYaw));

            LightDirection = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(x, y, z));
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PolygonCountString))]
        private int _polygonCount;

        public string PolygonCountString => $"Polygons: {PolygonCount:N0}";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(VertexCountString))]
        private int _vertexCount;

        public string VertexCountString => $"Vertices: {VertexCount:N0}";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UvSetCountString))]
        private int _uvSetCount;

        public string UvSetCountString => $"UV Sets: {UvSetCount}";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MaterialCountString))]
        private int _materialCount;

        public string MaterialCountString => $"Materials: {MaterialCount}";

        [ObservableProperty]
        private string _boundsString = "--";

        [RelayCommand]
        public void ZoomCamera(float delta)
        {
            _camera.Zoom(delta);
        }

        partial void OnModelPathChanged(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
            {
                _ = LoadModelAsync(value);
            }
        }
        
        public async Task LoadModelAsync(string path)
        {
            if (_renderer == null)
            {
                ErrorMessage = "Renderer not initialized";
                HasError = true;
                return;
            }
            
            IsLoading = true;
            HasError = false;
            ErrorMessage = null;
            
            try
            {
                // Load model data on background thread
                Pivot.Utilities.MeshData? meshData = null;
                await Task.Run(() =>
                {
                    using var loader = new Pivot.Utilities.ModelLoader();
                    meshData = loader.LoadModel(path);
                });
                
                if (meshData != null)
                {
                    // Upload to GPU (must be on UI thread)
                    _renderer.UploadMesh(meshData.Vertices, meshData.Indices);
                    
                    // Store bounds and fit camera
                    _currentBounds = meshData.Bounds;
                    _camera.FitToBounds(_currentBounds);

                    // Update stats
                    PolygonCount = meshData.Indices.Length / 3;
                    VertexCount = meshData.Vertices.Length;
                    UvSetCount = meshData.UVSetCount;
                    MaterialCount = meshData.MaterialCount;
                    var size = _currentBounds.Size;
                    BoundsString = $"Bounds: {size.X:F2} x {size.Y:F2} x {size.Z:F2}";
                }
                
                System.Diagnostics.Debug.WriteLine($"[ModelViewerViewModel] Loaded: {path}");
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                HasError = true;
                System.Diagnostics.Debug.WriteLine($"[ModelViewerViewModel] Error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
