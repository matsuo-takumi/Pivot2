using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Pivot.Utilities;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Pivot.Controls
{
    public sealed partial class ModelViewerControl : UserControl
    {
        private BoundingBox _currentBounds;
        private bool _hasBounds = false;

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
            CompositionTarget.Rendering += OnRenderFrame;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Ensure keyboard focus is possible
            this.Focus(FocusState.Programmatic);
        }

        private void OnRenderFrame(object? sender, object e)
        {
            // Update axis gizmo every frame to sync with camera
            var panel = VulkanPanel as VulkanSwapChainPanel;
            if (panel?.Camera != null)
            {
                AxisGizmoControl.UpdateFromCamera(panel.Camera);
            }
        }

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
            
            var panel = VulkanPanel as VulkanSwapChainPanel;
            if (panel?.Camera != null)
            {
                panel.Camera.FitToBounds(_currentBounds);
            }
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
                // Show loading indicator
                LoadingIndicator.Visibility = Visibility.Visible;
                ErrorMessageBorder.Visibility = Visibility.Collapsed;

                // Get panel for renderer and camera
                var panel = VulkanPanel as VulkanSwapChainPanel;
                if (panel?.Renderer == null)
                {
                    ShowError("Renderer not initialized", "VulkanSwapChainPanel is not ready.");
                    return;
                }

                // Load model data on background thread (CPU work only)
                MeshData? meshData = null;
                await Task.Run(() =>
                {
                    using var loader = new ModelLoader();
                    meshData = loader.LoadModel(path);
                });

                // Upload to GPU on UI thread (synchronized with render loop)
                if (meshData != null)
                {
                    panel.Renderer.UploadMesh(meshData.Vertices, meshData.Indices);
                    
                    // Store bounds for F key fit
                    _currentBounds = meshData.Bounds;
                    _hasBounds = true;
                    
                    // Fit camera to model bounds
                    panel.Camera.FitToBounds(meshData.Bounds);
                }

                LoadingIndicator.Visibility = Visibility.Collapsed;
                System.Diagnostics.Debug.WriteLine($"[ModelViewerControl] Loaded model: {path}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelViewerControl] Failed to load model: {ex.Message}");
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

