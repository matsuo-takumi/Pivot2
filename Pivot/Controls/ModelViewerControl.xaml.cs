using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HelixToolkit.WinUI;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using Windows.ApplicationModel;

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
            ViewerBackground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            this.Unloaded += ModelViewerControl_Unloaded;
            
            // Viewport3DのBackgroundを設定
            var viewport = this.FindName("Viewport3D") as HelixToolkit.WinUI.Viewport3DX;
            if (viewport != null)
            {
                viewport.Background = ViewerBackground;
            }
            
            // DirectionalLightのDirectionをコードビハインドで設定
            var directionalLight = this.FindName("DirectionalLight") as HelixToolkit.WinUI.DirectionalLight3D;
            if (directionalLight != null)
            {
                directionalLight.Direction = new SharpDX.Vector3(0, 0, -1);
            }
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

                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        ShowLoading(false);
                        ShowError(false);
                        var modelGroup = this.FindName("ModelGroup") as HelixToolkit.WinUI.GroupModel3D;
                        if (modelGroup != null && modelGroup.Items != null)
                        {
                            modelGroup.Items.Clear();
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
                System.Collections.Generic.List<HelixToolkit.SharpDX.Core.Object3D>? objModels = null;
                HelixToolkit.SharpDX.Core.Model.Scene.SceneNode? assimpScene = null;
                
                await Task.Run(() =>
                {
                    try
                    {
                        if (ct.IsCancellationRequested) return;

                        if (ext == ".obj")
                        {
                            var reader = new HelixToolkit.SharpDX.Core.ObjReader();
                            objModels = reader.Read(filePath);
                        }
                        else if (ext == ".fbx" || ext == ".glb")
                        {
                            // Assimpを使用してFBX/GLB形式を読み込む
                            // アセンブリ名を完全修飾名で指定して読み込む
                            try
                            {
                                Assembly? assimpAssembly = null;
                                
                                // WinUIアプリでは、複数の場所からアセンブリを読み込む
                                var searchPaths = new List<string>();
                                
                                // 1. Package.Current.InstalledLocation
                                try
                                {
                                    var packageLocation = Package.Current.InstalledLocation.Path;
                                    searchPaths.Add(packageLocation);
                                }
                                catch { }
                                
                                // 2. 現在のアセンブリの場所
                                try
                                {
                                    var currentAssembly = Assembly.GetExecutingAssembly();
                                    var assemblyLocation = Path.GetDirectoryName(currentAssembly.Location);
                                    if (!string.IsNullOrEmpty(assemblyLocation))
                                    {
                                        searchPaths.Add(assemblyLocation);
                                    }
                                }
                                catch { }
                                
                                // 3. AppXフォルダ（デバッグ時）
                                try
                                {
                                    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                                    var appXPath = Path.Combine(appDataPath, "Packages", Package.Current.Id.FamilyName, "LocalCache", "Local", "Microsoft", "Windows", "WinX");
                                    if (Directory.Exists(appXPath))
                                    {
                                        searchPaths.Add(appXPath);
                                    }
                                }
                                catch { }
                                
                                // 各パスを試す
                                foreach (var searchPath in searchPaths)
                                {
                                    try
                                    {
                                        var assimpDllPath = Path.Combine(searchPath, "HelixToolkit.SharpDX.Assimp.dll");
                                        if (File.Exists(assimpDllPath))
                                        {
                                            assimpAssembly = Assembly.LoadFrom(assimpDllPath);
                                            System.Diagnostics.Debug.WriteLine($"ModelViewerControl: Assimpアセンブリを読み込みました: {assimpDllPath}");
                                            break;
                                        }
                                    }
                                    catch { }
                                }
                                
                                // 最後のフォールバック: アセンブリ名で読み込む
                                if (assimpAssembly == null)
                                {
                                    try
                                    {
                                        assimpAssembly = Assembly.Load(new AssemblyName("HelixToolkit.SharpDX.Assimp"));
                                        System.Diagnostics.Debug.WriteLine("ModelViewerControl: Assimpアセンブリをアセンブリ名で読み込みました");
                                    }
                                    catch (Exception loadEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"ModelViewerControl: Assimpアセンブリの読み込みに失敗: {loadEx.Message}");
                                    }
                                }
                                
                                if (assimpAssembly != null)
                                {
                                    var importerType = assimpAssembly.GetType("HelixToolkit.SharpDX.Assimp.AssimpImporter");
                                    if (importerType != null)
                                    {
                                        var importer = Activator.CreateInstance(importerType);
                                        var loadMethod = importerType.GetMethod("Load", new[] { typeof(string) });
                                        if (loadMethod != null)
                                        {
                                            assimpScene = loadMethod.Invoke(importer, new object[] { filePath }) as HelixToolkit.SharpDX.Core.Model.Scene.SceneNode;
                                        }
                                    }
                                }
                            }
                            catch (Exception assimpEx)
                            {
                                errorMessage = $"AssimpImporterの読み込みに失敗しました: {assimpEx.Message}";
                                System.Diagnostics.Debug.WriteLine($"ModelViewerControl: {errorMessage}");
                                System.Diagnostics.Debug.WriteLine($"ModelViewerControl: {ext}形式は現在サポートされていません。");
                                
                                // アセンブリが見つからない場合の詳細情報
                                if (assimpEx is FileNotFoundException || assimpEx is System.IO.FileNotFoundException)
                                {
                                    errorMessage = $"HelixToolkit.SharpDX.Assimpアセンブリが見つかりません。\n{ext}形式の読み込みにはこのアセンブリが必要です。";
                                }
                            }
                        }
                        else
                        {
                            errorMessage = $"サポートされていない形式: {ext}";
                            System.Diagnostics.Debug.WriteLine($"ModelViewerControl: {errorMessage}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ModelViewerControl.LoadModelAsync Task.Run error: {ex.Message}");
                    }
                });

                if (ct.IsCancellationRequested) return;

                // UIスレッドでモデルを追加
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (ct.IsCancellationRequested) return;

                    try
                    {
                        ShowLoading(false);
                        
                        var viewport = this.FindName("Viewport3D") as HelixToolkit.WinUI.Viewport3DX;
                        var modelGroup = this.FindName("ModelGroup") as HelixToolkit.WinUI.GroupModel3D;
                        if (viewport == null || modelGroup == null || modelGroup.Items == null) return;

                        modelGroup.Items.Clear();

                        bool modelLoaded = false;
                        
                        // ObjReaderで読み込んだモデルを追加
                        if (objModels != null && objModels.Count > 0)
                        {
                            foreach (var model in objModels)
                            {
                                modelGroup.Items.Add(model);
                            }
                            modelLoaded = true;
                        }
                        // AssimpImporterで読み込んだシーンを追加
                        else if (assimpScene != null)
                        {
                            modelGroup.Items.Add(assimpScene);
                            modelLoaded = true;
                        }

                        if (modelLoaded)
                        {
                            if (viewport.Camera != null)
                            {
                                viewport.Camera.ZoomExtents(viewport, 0);
                            }
                            ShowError(false, null);
                        }
                        else
                        {
                            // モデルが読み込めなかった場合（サポートされていない形式など）
                            ShowError(true, errorMessage ?? $"モデルの読み込みに失敗しました。\n形式: {ext}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ModelViewerControl.LoadModelAsync UI update error: {ex.Message}");
                        ShowError(true, $"UI更新エラー: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModelViewerControl.LoadModelAsync error: {ex.Message}");
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
                var modelGroup = this.FindName("ModelGroup") as HelixToolkit.WinUI.GroupModel3D;
                if (modelGroup != null && modelGroup.Items != null)
                {
                    modelGroup.Items.Clear();
                }
            }
            catch { }
        }
    }
}

