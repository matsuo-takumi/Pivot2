using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HelixToolkit.WinUI;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HelixToolkit.SharpDX.Core.Assimp;
using System.Text.Json;
using System.Text;

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
        private const string DebugLogPath = @"c:\Users\ta_matsuo\source\repos\matsuo-takumi\Pivot2\.cursor\debug.log";
        private const string DebugSessionId = "debug-session";
        private const string DebugRunId = "run1";

        public ModelViewerControl()
        {
            this.InitializeComponent();
            ViewerBackground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            this.Unloaded += ModelViewerControl_Unloaded;
            
            // Viewport3DのBackgroundを設定
            var viewport = this.FindName("Viewport3D") as HelixToolkit.WinUI.Viewport3DX;
            if (viewport != null)
            {
                viewport.Background = ViewerBackground;
                
                // EffectsManagerを設定（レンダリングに必須）
                viewport.EffectsManager = new HelixToolkit.SharpDX.Core.DefaultEffectsManager();
            }
            
            // DirectionalLightのDirectionをコードビハインドで設定
            var directionalLight = this.FindName("DirectionalLight") as HelixToolkit.WinUI.DirectionalLight3D;
            if (directionalLight != null)
            {
                directionalLight.Direction = new SharpDX.Vector3(0, 0, -1);
            }

            // Cameraを設定
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

            // グリッドを作成
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

            // Minor grid lines (lighter)
            for (float i = -gridSize; i <= gridSize; i += minorStep)
            {
                if (Math.Abs(i % majorStep) > 0.001f) // Skip major lines
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

            // Major grid lines (brighter)
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

            // GroupModel3Dでまとめる
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
                    viewport.Background = e.NewValue as Microsoft.UI.Xaml.Media.Brush ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
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

                LogDebug("H1", "LoadModelAsync entry", new { filePath, exists = !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath) });

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

                // モデルファイルの読み込みはバックグラウンドスレッドで実行
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
                                // HelixToolkit.SharpDX.Core.Assimp 2.25.0 では Importer クラスを使用する
                                var importer = new Importer();
                                assimpScene = importer.Load(filePath);
                                LogDebug("H2", "Assimp importer result", new
                                {
                                    hasScene = assimpScene != null,
                                    sceneType = assimpScene?.Root?.GetType().Name,
                                    rootNull = assimpScene?.Root == null
                                });
                            }
                            catch (Exception assimpEx)
                            {
                                errorMessage = $"Assimp importer の読み込みに失敗しました: {assimpEx.Message}";
                                System.Diagnostics.Debug.WriteLine($"ModelViewerControl: {errorMessage}");
                                System.Diagnostics.Debug.WriteLine($"ModelViewerControl: {ext}形式は現在サポートされていません。");
                            }
                        }
                        else
                        {
                            errorMessage = $"サポートされていない形式: {ext}";
                            System.Diagnostics.Debug.WriteLine($"ModelViewerControl: {errorMessage}");
                            LogDebug("H3", "Unsupported extension", new { ext });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ModelViewerControl.LoadModelAsync Task.Run error: {ex.Message}");
                        LogDebug("H4", "Background exception", new { ex.Message, ex.StackTrace });
                    }
                });

                if (ct.IsCancellationRequested) return;

                // UIスレッドでモデルを追加
                var uiEnqueue = DispatcherQueue.TryEnqueue(() =>
                {
                    LogDebug("H7", "UI dispatcher entered", new { filePath, ext, ctCanceled = ct.IsCancellationRequested });
                    if (ct.IsCancellationRequested) return;

                    try
                    {
                        ShowLoading(false);
                        
                        var viewport = this.FindName("Viewport3D") as HelixToolkit.WinUI.Viewport3DX;
                               var presenter = this.FindName("ModelPresenter") as HelixToolkit.WinUI.Element3DPresenter;
                               if (viewport == null || presenter == null)
                        {
                                   LogDebug("H7", "UI dispatcher missing elements", new { viewportNull = viewport == null, presenterNull = presenter == null });
                            return;
                        }

                               presenter.Content = null;

                        bool modelLoaded = false;
                        
                        // AssimpImporterで読み込んだシーンを追加
                        if (assimpScene?.Root != null)
                        {
                                   var group = new HelixToolkit.WinUI.SceneNodeGroupModel3D();
                                   group.AddNode(assimpScene.Root);
                                   presenter.Content = group;
                            modelLoaded = true;
                            
                            // 詳細なデバッグ情報を出力
                            System.Diagnostics.Debug.WriteLine($"ModelViewerControl: Root node type: {assimpScene.Root.GetType().Name}");
                            System.Diagnostics.Debug.WriteLine($"ModelViewerControl: Root node Name: {assimpScene.Root.Name}");
                            System.Diagnostics.Debug.WriteLine($"ModelViewerControl: Has Animations: {assimpScene.HasAnimation}, Animation Count: {assimpScene.Animations?.Count ?? 0}");
                            
                            // 子ノードの数を確認
                            int nodeCount = CountNodes(assimpScene.Root);
                            System.Diagnostics.Debug.WriteLine($"ModelViewerControl: Total node count: {nodeCount}");
                            System.Diagnostics.Debug.WriteLine($"ModelViewerControl: Group SceneNode count: {group.GroupNode?.Items?.Count ?? 0}");
                                   LogDebug("H6", "Set presenter content", new
                            {
                                       hasContent = presenter.Content != null,
                                       rootType = assimpScene.Root.GetType().Name,
                                nodeCount
                            });
                        }

                        if (modelLoaded)
                        {
                            System.Diagnostics.Debug.WriteLine($"ModelViewerControl: Model loaded successfully. Calling ZoomExtents.");
                            if (viewport.Camera != null)
                            {
                                // ZoomExtentsを少し遅延して呼び出す（モデルがシーンに追加された後に実行）
                                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                                {
                                    try
                                    {
                                        viewport.Camera.ZoomExtents(viewport, 500);
                                        System.Diagnostics.Debug.WriteLine($"ModelViewerControl: ZoomExtents called. Camera Position: {viewport.Camera.Position}");
                                    }
                                    catch (Exception zoomEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"ModelViewerControl: ZoomExtents failed: {zoomEx.Message}");
                                    }
                                });
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"ModelViewerControl: Camera is null, cannot call ZoomExtents.");
                            }
                            ShowError(false, null);
                        }
                        else
                        {
                            // モデルが読み込めなかった場合（サポートされていない形式など）
                            ShowError(true, errorMessage ?? $"モデルの読み込みに失敗しました。\n形式: {ext}");
                        }
                               LogDebug("H5", "UI update result", new { modelLoaded, hasContent = presenter.Content != null, cameraNull = viewport?.Camera == null, errorMessage });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ModelViewerControl.LoadModelAsync UI update error: {ex.Message}");
                        ShowError(true, $"UI更新エラー: {ex.Message}");
                        LogDebug("H4", "UI exception", new { ex.Message, ex.StackTrace });
                    }
                });

                if (!uiEnqueue)
                {
                    LogDebug("H7", "UI dispatcher enqueue failed", new { filePath, ext });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModelViewerControl.LoadModelAsync error: {ex.Message}");
                DispatcherQueue.TryEnqueue(() =>
                {
                    ShowLoading(false);
                    ShowError(true, $"エラー: {ex.Message}");
                });
                LogDebug("H4", "Outer exception", new { ex.Message, ex.StackTrace });
            }
        }

        // #region agent log
        private void LogDebug(string hypothesisId, string message, object data)
        {
            try
            {
                var payload = new
                {
                    sessionId = DebugSessionId,
                    runId = DebugRunId,
                    hypothesisId,
                    location = "ModelViewerControl.xaml.cs",
                    message,
                    data,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                var json = JsonSerializer.Serialize(payload);
                File.AppendAllText(DebugLogPath, json + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // swallow logging errors
            }
        }
        // #endregion

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

        private static int CountNodes(HelixToolkit.SharpDX.Core.Model.Scene.SceneNode node)
        {
            if (node == null) return 0;
            int count = 1;
            if (node.Items != null)
            {
                foreach (var child in node.Items)
                {
                    count += CountNodes(child);
                }
            }
            return count;
        }
    }
}

