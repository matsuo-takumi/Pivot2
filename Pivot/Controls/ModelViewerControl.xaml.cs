using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HelixToolkit.WinUI;
using SharpDX;
using System;
using System.IO;
using System.Threading.Tasks;
using HelixToolkit.SharpDX.Core.Assimp;

namespace Pivot.Controls
{
    public sealed partial class ModelViewerControl : UserControl
    {
        public static readonly DependencyProperty ViewerBackgroundProperty =
            DependencyProperty.Register(nameof(ViewerBackground), typeof(Microsoft.UI.Xaml.Media.Brush),
                typeof(ModelViewerControl),
                new PropertyMetadata(null, OnViewerBackgroundChanged));

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

        private System.Threading.CancellationTokenSource? _loadCts;

        public ModelViewerControl()
        {
            this.InitializeComponent();
            this.Unloaded += ModelViewerControl_Unloaded;
            
            var viewport = this.FindName("Viewport3D") as HelixToolkit.WinUI.Viewport3DX;
            if (viewport != null)
            {
                viewport.EffectsManager = new HelixToolkit.SharpDX.Core.DefaultEffectsManager();
            }
            
            var directionalLight = this.FindName("DirectionalLight") as HelixToolkit.WinUI.DirectionalLight3D;
            if (directionalLight != null)
            {
                directionalLight.Direction = new SharpDX.Vector3(0, 0, -1);
            }

            if (viewport != null)
            {
                viewport.Camera = new HelixToolkit.WinUI.PerspectiveCamera
                {
                    Position = new SharpDX.Vector3(0, 10, 20),
                    LookDirection = new SharpDX.Vector3(0, -10, -20),
                    UpDirection = new SharpDX.Vector3(0, 1, 0),
                    FarPlaneDistance = 5000,
                    NearPlaneDistance = 0.1
                };
            }

            CreateGrid();
        }

        private void CreateGrid()
        {
            var gridPresenter = this.FindName("GridPresenter") as HelixToolkit.WinUI.Element3DPresenter;
            if (gridPresenter == null) return;

            var lineBuilder = new HelixToolkit.SharpDX.Core.LineBuilder();
            
            float gridSize = 50f;
            float majorStep = 10f;
            float minorStep = 1f;

            for (float i = -gridSize; i <= gridSize; i += minorStep)
            {
                if (Math.Abs(i % majorStep) > 0.001f)
                {
                    lineBuilder.AddLine(new Vector3(i, 0, -gridSize), new Vector3(i, 0, gridSize));
                    lineBuilder.AddLine(new Vector3(-gridSize, 0, i), new Vector3(gridSize, 0, i));
                }
            }

            var minorGridGeometry = lineBuilder.ToLineGeometry3D();
            var minorGridModel = new HelixToolkit.WinUI.LineGeometryModel3D
            {
                Geometry = minorGridGeometry,
                Color = Windows.UI.Color.FromArgb(255, 77, 77, 77),
                Thickness = 0.5
            };

            var majorLineBuilder = new HelixToolkit.SharpDX.Core.LineBuilder();
            for (float i = -gridSize; i <= gridSize; i += majorStep)
            {
                majorLineBuilder.AddLine(new Vector3(i, 0, -gridSize), new Vector3(i, 0, gridSize));
                majorLineBuilder.AddLine(new Vector3(-gridSize, 0, i), new Vector3(gridSize, 0, i));
            }

            var majorGridGeometry = majorLineBuilder.ToLineGeometry3D();
            var majorGridModel = new HelixToolkit.WinUI.LineGeometryModel3D
            {
                Geometry = majorGridGeometry,
                Color = Windows.UI.Color.FromArgb(255, 128, 128, 128),
                Thickness = 1.0
            };

            var gridGroup = new HelixToolkit.WinUI.GroupModel3D();
            gridGroup.Children.Add(minorGridModel);
            gridGroup.Children.Add(majorGridModel);

            gridPresenter.Content = gridGroup;
        }

        private static void OnViewerBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ModelViewerControl control)
            {
                var viewport = control.FindName("Viewport3D") as HelixToolkit.WinUI.Viewport3DX;
                if (viewport != null)
                {
                    viewport.Background = e.NewValue as Microsoft.UI.Xaml.Media.Brush 
                        ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                }
            }
        }

        private static void OnModelPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ModelViewerControl control)
            {
                _ = control.LoadModelAsync((string?)e.NewValue);
            }
        }

        private async Task LoadModelAsync(string? filePath)
        {
            try
            {
                _loadCts?.Cancel();
                _loadCts = new System.Threading.CancellationTokenSource();
                var ct = _loadCts.Token;

                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        ShowLoading(false);
                        ShowError(false);
                        var presenter = this.FindName("ModelPresenter") as HelixToolkit.WinUI.Element3DPresenter;
                        if (presenter != null)
                        {
                            presenter.Content = null;
                        }
                    });
                    return;
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    ShowError(false);
                    ShowLoading(true);
                });

                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                string? errorMessage = null;
                HelixToolkit.SharpDX.Core.Assimp.HelixToolkitScene? assimpScene = null;
                
                await Task.Run(() =>
                {
                    try
                    {
                        if (ct.IsCancellationRequested) return;

                        if (ext == ".fbx" || ext == ".glb" || ext == ".obj")
                        {
                            try
                            {
                                var importer = new Importer();
                                assimpScene = importer.Load(filePath);
                            }
                            catch (Exception assimpEx)
                            {
                                errorMessage = $"モデルの読み込みに失敗しました: {assimpEx.Message}";
                            }
                        }
                        else
                        {
                            errorMessage = $"サポートされていない形式: {ext}";
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessage = $"読み込みエラー: {ex.Message}";
                    }
                });

                if (ct.IsCancellationRequested) return;

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (ct.IsCancellationRequested) return;

                    try
                    {
                        ShowLoading(false);
                        
                        var viewport = this.FindName("Viewport3D") as HelixToolkit.WinUI.Viewport3DX;
                        var presenter = this.FindName("ModelPresenter") as HelixToolkit.WinUI.Element3DPresenter;
                        if (viewport == null || presenter == null) return;

                        presenter.Content = null;
                        bool modelLoaded = false;
                        
                        if (assimpScene?.Root != null)
                        {
                            var group = new HelixToolkit.WinUI.SceneNodeGroupModel3D();
                            group.AddNode(assimpScene.Root);
                            presenter.Content = group;
                            modelLoaded = true;
                        }

                        if (modelLoaded)
                        {
                            if (viewport.Camera != null)
                            {
                                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                                {
                                    try
                                    {
                                        viewport.Camera.ZoomExtents(viewport, 500);
                                    }
                                    catch { }
                                });
                            }
                            ShowError(false);
                        }
                        else
                        {
                            ShowError(true, errorMessage ?? $"モデルの読み込みに失敗しました。\n形式: {ext}");
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowError(true, $"表示エラー: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ShowLoading(false);
                    ShowError(true, $"エラー: {ex.Message}");
                });
            }
        }

        private void ShowError(bool show, string? detailMessage = null)
        {
            var errorBorder = this.FindName("ErrorMessageBorder") as Border;
            var errorText = this.FindName("ErrorMessageText") as TextBlock;
            var errorDetail = this.FindName("ErrorDetailText") as TextBlock;
            
            if (errorBorder != null)
            {
                errorBorder.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
            
            if (errorText != null && show)
            {
                errorText.Text = "モデルの読み込みに失敗しました";
            }
            
            if (errorDetail != null)
            {
                if (show && !string.IsNullOrWhiteSpace(detailMessage))
                {
                    errorDetail.Text = detailMessage;
                    errorDetail.Visibility = Visibility.Visible;
                }
                else
                {
                    errorDetail.Visibility = Visibility.Collapsed;
                }
            }
        }
        
        private void ShowLoading(bool show)
        {
            var loadingIndicator = this.FindName("LoadingIndicator") as Border;
            if (loadingIndicator != null)
            {
                loadingIndicator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ModelViewerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _loadCts?.Cancel();
                var presenter = this.FindName("ModelPresenter") as HelixToolkit.WinUI.Element3DPresenter;
                if (presenter != null)
                {
                    presenter.Content = null;
                }
            }
            catch { }
        }
    }
}
