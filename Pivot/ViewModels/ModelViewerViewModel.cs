using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        
        [RelayCommand]
        public void ZoomCamera(float delta)
        {
            _camera.Zoom(delta);
        }
    }
}
