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
        private ViewportSettingsService? _viewportSettings;
        private MaterialSettingsService? _materialSettings;
        private ViewportBackgroundMode _backgroundMode = ViewportBackgroundMode.Custom;

        public ModelViewerViewModel()
        {
            _viewportSettings = App.Current?.Services?.GetService<ViewportSettingsService>();
            _materialSettings = App.Current?.Services?.GetService<MaterialSettingsService>();
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
            else if (message.Value == "Viewport.WireframeColor")
            {
                InitializeWireframeColorFromSettings();
            }
        }

        private void OnThemeChanged(Pivot.Messages.ThemeChangedMessage message)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ModelViewerViewModel] OnThemeChanged: {message.Value}, _backgroundMode={_backgroundMode}");
                // テーマ変更時はMatchThemeの場合のみ再適用
                if (_backgroundMode == ViewportBackgroundMode.MatchTheme)
                {
                    System.Diagnostics.Debug.WriteLine($"[ModelViewerViewModel] Applying theme-based background");
                    ApplyThemeBasedBackground();
                    System.Diagnostics.Debug.WriteLine($"[ModelViewerViewModel] ApplyThemeBasedBackground completed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelViewerViewModel] OnThemeChanged ERROR: {ex}");
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
            InitializeWireframeColorFromSettings();
            
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
                _viewportSettings ??= App.Current?.Services?.GetService<ViewportSettingsService>();
                if (_viewportSettings == null) return;
                
                _backgroundMode = _viewportSettings.GetBackgroundMode();
                
                if (_backgroundMode == ViewportBackgroundMode.MatchTheme)
                {
                    ApplyThemeBasedBackground();
                }
                else
                {
                    var hexColor = _viewportSettings.GetBackgroundColor();
                    BackgroundColor = HexToColor(hexColor);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelViewerViewModel] InitializeBackgroundFromSettings error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Initialize wireframe color from settings
        /// </summary>
        private void InitializeWireframeColorFromSettings()
        {
            try
            {
                _viewportSettings ??= App.Current?.Services?.GetService<ViewportSettingsService>();
                if (_viewportSettings == null) return;
                
                var hexColor = _viewportSettings.GetWireframeColor();
                WireframeColor = HexToColor(hexColor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelViewerViewModel] InitializeWireframeColorFromSettings error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Apply background color based on current Mica backdrop style and theme
        /// </summary>
        public void ApplyThemeBasedBackground()
        {
            try
            {
                _viewportSettings ??= App.Current?.Services?.GetService<ViewportSettingsService>();
                _backgroundMode = _viewportSettings?.GetBackgroundMode() ?? ViewportBackgroundMode.Custom;
                
                if (_backgroundMode != ViewportBackgroundMode.MatchTheme)
                {
                    // Use custom color from settings
                    var hexColor = _viewportSettings?.GetBackgroundColor() ?? "#3399CC";
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
                _materialSettings ??= App.Current?.Services?.GetService<MaterialSettingsService>();
                if (_materialSettings == null || _renderer == null) return;
                
                var mp = _materialSettings.GetMaterialParams();
                _renderer.SetMaterialParams(mp.AlbedoR, mp.AlbedoG, mp.AlbedoB, mp.Metallic, mp.Roughness);
                
                System.Diagnostics.Debug.WriteLine($"[ModelViewerViewModel] Applied material params: RGB({mp.AlbedoR:F2},{mp.AlbedoG:F2},{mp.AlbedoB:F2}) M={mp.Metallic:F2} R={mp.Roughness:F2}");
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
        private Pivot.Models.ShadingMode _currentShadingMode = Pivot.Models.ShadingMode.Combined;

        // Shader toggle properties
        [ObservableProperty]
        private bool _useTexture = false;

        [ObservableProperty]
        private bool _useVertexColor = false;

        [ObservableProperty]
        private bool _useUVChecker = false;

        [ObservableProperty]
        private bool _useMaterial = true;

        [ObservableProperty]
        private bool _useWireframe = false;

        partial void OnCurrentShadingModeChanged(Pivot.Models.ShadingMode value)
        {
            _renderer?.SetShadingMode(value);
            ApplyShaderToggles();
        }

        partial void OnUseTextureChanged(bool value)
        {
            ApplyShaderToggles();
        }

        partial void OnUseVertexColorChanged(bool value)
        {
            ApplyShaderToggles();
        }

        partial void OnUseUVCheckerChanged(bool value)
        {
            ApplyShaderToggles();
        }

        partial void OnUseMaterialChanged(bool value)
        {
            ApplyShaderToggles();
        }

        partial void OnUseWireframeChanged(bool value)
        {
            ApplyShaderToggles();
        }

        /// <summary>
        /// Apply current shader toggle states to the renderer
        /// </summary>
        private void ApplyShaderToggles()
        {
            _renderer?.SetShaderToggles(UseTexture, UseVertexColor, UseUVChecker, UseMaterial, UseWireframe);
        }

        [RelayCommand]
        public void ToggleTexture()
        {
            UseTexture = !UseTexture;
        }

        [RelayCommand]
        public void ToggleVertexColor()
        {
            UseVertexColor = !UseVertexColor;
        }

        [RelayCommand]
        public void ToggleUVChecker()
        {
            UseUVChecker = !UseUVChecker;
        }

        [RelayCommand]
        public void ToggleWireframe()
        {
            UseWireframe = !UseWireframe;
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
        private Windows.UI.Color _wireframeColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);

        partial void OnWireframeColorChanged(Windows.UI.Color value)
        {
            _renderer?.SetWireframeColor(value.R / 255f, value.G / 255f, value.B / 255f);
        }

        // Key Light (Main directional light) - Z inverted for front-facing
        [ObservableProperty]
        private System.Numerics.Vector3 _lightDirection = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(0.5f, 1.0f, -0.3f));

        [ObservableProperty]
        private float _lightIntensity = 1.0f;

        [ObservableProperty]
        private Windows.UI.Color _lightColor = Windows.UI.Color.FromArgb(255, 255, 255, 255); // White

        // Spherical coordinates for key light (yaw/pitch in radians)
        [ObservableProperty]
        private float _lightYaw = 0.3f;

        [ObservableProperty]
        private float _lightPitch = 1.0f;

        // Ambient Light
        [ObservableProperty]
        private float _ambientIntensity = 0.1f;

        [ObservableProperty]
        private Windows.UI.Color _ambientColor = Windows.UI.Color.FromArgb(255, 102, 102, 128);

        // Rim Light
        [ObservableProperty]
        private float _rimIntensity = 0.3f;

        [ObservableProperty]
        private Windows.UI.Color _rimColor = Windows.UI.Color.FromArgb(255, 204, 230, 255);

        // Back Light
        [ObservableProperty]
        private float _backIntensity = 0.2f;

        [ObservableProperty]
        private Windows.UI.Color _backColor = Windows.UI.Color.FromArgb(255, 128, 128, 153);

        partial void OnLightYawChanged(float value)
        {
            UpdateLightDirectionFromSpherical();
        }

        partial void OnLightPitchChanged(float value)
        {
            UpdateLightDirectionFromSpherical();
        }

        /// <summary>
        /// Update light direction from yaw/pitch spherical coordinates
        /// </summary>
        private void UpdateLightDirectionFromSpherical()
        {
            // Convert spherical to cartesian
            float x = (float)(Math.Sin(_lightPitch) * Math.Cos(_lightYaw));
            float y = (float)Math.Cos(_lightPitch);
            float z = (float)(Math.Sin(_lightPitch) * Math.Sin(_lightYaw));

            LightDirection = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(x, y, z));
        }

        /// <summary>
        /// Rotate the light direction by yaw/pitch deltas (called during Ctrl+L drag)
        /// </summary>
        public void RotateLight(float deltaYaw, float deltaPitch)
        {
            LightYaw += deltaYaw;
            LightPitch = Math.Clamp(LightPitch + deltaPitch, 0.1f, (float)Math.PI - 0.1f);
        }

        partial void OnBackgroundColorChanged(Windows.UI.Color value)
        {
            _renderer?.SetBackgroundColor(value.R / 255f, value.G / 255f, value.B / 255f, value.A / 255f);
        }

        // Key Light changed handlers
        partial void OnLightDirectionChanged(System.Numerics.Vector3 value)
        {
            _renderer?.SetKeyLightParams(value, LightIntensity, new System.Numerics.Vector3(LightColor.R / 255f, LightColor.G / 255f, LightColor.B / 255f));
        }

        partial void OnLightIntensityChanged(float value)
        {
            _renderer?.SetKeyLightParams(LightDirection, value, new System.Numerics.Vector3(LightColor.R / 255f, LightColor.G / 255f, LightColor.B / 255f));
        }

        partial void OnLightColorChanged(Windows.UI.Color value)
        {
            _renderer?.SetKeyLightParams(LightDirection, LightIntensity, new System.Numerics.Vector3(value.R / 255f, value.G / 255f, value.B / 255f));
        }

        // Ambient Light changed handlers
        partial void OnAmbientIntensityChanged(float value)
        {
            _renderer?.SetAmbientLight(value, new System.Numerics.Vector3(AmbientColor.R / 255f, AmbientColor.G / 255f, AmbientColor.B / 255f));
        }

        partial void OnAmbientColorChanged(Windows.UI.Color value)
        {
            _renderer?.SetAmbientLight(AmbientIntensity, new System.Numerics.Vector3(value.R / 255f, value.G / 255f, value.B / 255f));
        }

        // Rim Light changed handlers
        partial void OnRimIntensityChanged(float value)
        {
            _renderer?.SetRimLight(value, new System.Numerics.Vector3(RimColor.R / 255f, RimColor.G / 255f, RimColor.B / 255f));
        }

        partial void OnRimColorChanged(Windows.UI.Color value)
        {
            _renderer?.SetRimLight(RimIntensity, new System.Numerics.Vector3(value.R / 255f, value.G / 255f, value.B / 255f));
        }

        // Back Light changed handlers
        partial void OnBackIntensityChanged(float value)
        {
            _renderer?.SetBackLight(value, new System.Numerics.Vector3(BackColor.R / 255f, BackColor.G / 255f, BackColor.B / 255f));
        }

        partial void OnBackColorChanged(Windows.UI.Color value)
        {
            _renderer?.SetBackLight(BackIntensity, new System.Numerics.Vector3(value.R / 255f, value.G / 255f, value.B / 255f));
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

        /// <summary>
        /// Save current lighting state to settings
        /// </summary>
        public async Task SaveLightingStateAsync()
        {
            if (_materialSettings == null) return;
            
            var state = new LightingPreset
            {
                Name = "Last Used",
                KeyIntensity = LightIntensity,
                KeyColor = LightColor,
                KeyYaw = LightYaw,
                KeyPitch = LightPitch,
                AmbientIntensity = AmbientIntensity,
                AmbientColor = AmbientColor,
                RimIntensity = RimIntensity,
                RimColor = RimColor,
                BackIntensity = BackIntensity,
                BackColor = BackColor
            };
            
            await _materialSettings.SaveLightingStateAsync(state);
        }

        /// <summary>
        /// Restore lighting state from settings
        /// </summary>
        public async Task RestoreLightingStateAsync()
        {
            if (_materialSettings == null) return;
            
            var state = await _materialSettings.GetSavedLightingStateAsync();
            if (state != null)
            {
                ApplyLightingPreset(state);
            }
        }

        /// <summary>
        /// Apply a lighting preset to current state
        /// </summary>
        public void ApplyLightingPreset(LightingPreset preset)
        {
            LightIntensity = preset.KeyIntensity;
            LightColor = preset.KeyColor;
            LightYaw = preset.KeyYaw;
            LightPitch = preset.KeyPitch;
            AmbientIntensity = preset.AmbientIntensity;
            AmbientColor = preset.AmbientColor;
            RimIntensity = preset.RimIntensity;
            RimColor = preset.RimColor;
            BackIntensity = preset.BackIntensity;
            BackColor = preset.BackColor;
        }

        /// <summary>
        /// Create a LightingPreset from current state
        /// </summary>
        public LightingPreset CreateLightingPresetFromCurrent(string name)
        {
            return new LightingPreset
            {
                Name = name,
                IsCustom = true,
                KeyIntensity = LightIntensity,
                KeyColor = LightColor,
                KeyYaw = LightYaw,
                KeyPitch = LightPitch,
                AmbientIntensity = AmbientIntensity,
                AmbientColor = AmbientColor,
                RimIntensity = RimIntensity,
                RimColor = RimColor,
                BackIntensity = BackIntensity,
                BackColor = BackColor
            };
        }
    }
}
