using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Pivot.ViewModels;
using System.ComponentModel;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Services;
using Pivot.Models;
using System;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Pivot.Views
{
    public sealed partial class ImagePage : Page
    {
        public ImageViewModel ViewModel { get; set; }

        private TemplateItem? _lastSelectedItemForRange;
        private System.Threading.CancellationTokenSource? _borderThicknessUpdateCts;
        private const double DragActivationThresholdSquared = 16.0;
        private bool _isPointerDown = false;
        private bool _isDragIntent = false;
        private Windows.Foundation.Point _pointerDownPoint;
        private TemplateItem? _pressedItem;
        private bool _pendingSelectionForClick = false;
        private TemplateItem? _pendingSelectionItem;
        
        // 画像プレビュー用の変数
        private bool _isPanning = false;
        private Windows.Foundation.Point _lastPanPoint;
        private double _currentZoomFactor = 1.0;
        private bool _isPreviewLoading = false;
        private System.Threading.CancellationTokenSource? _previewLoadCts;
        private BitmapImage? _currentPreviewBitmap;
        private double _previewImageAspectRatio = 1.0;

        public ImagePage()
        {
            this.InitializeComponent();
            ViewModel = new ImageViewModel();
            this.DataContext = ViewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            // responsive handlers
            SizeChanged += ImagePage_SizeChanged;

            // 初期レイアウトを適用
            ApplyLayout(ViewModel.CurrentLayout);

            // 自動ロード: 設定に保存された ImageDirectories があればテスト用に読み込む（安全策: try/catch）
            try
            {
                var settings = App.Current.Services.GetService<SettingsService>();
                if (settings != null)
                {
                    var dirs = settings.GetUserSettings().ImageDirectories;
                    if (dirs != null && dirs.Count > 0)
                    {
                        _ = ViewModel.LoadFromDirectoriesAsync(dirs, 300);
                    }
                }
            }
            catch { }

            this.Unloaded += ImagePage_Unloaded;
            
            // 設定変更を監視
            UpdateBorderThicknessPeriodically();
        }
        
        private async void UpdateBorderThicknessPeriodically()
        {
            _borderThicknessUpdateCts = new System.Threading.CancellationTokenSource();
            var ct = _borderThicknessUpdateCts.Token;
            
            // 定期的に設定をチェックして更新（簡易的な実装）
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(500, ct); // 500msごとにチェック
                    if (ct.IsCancellationRequested) break;
                    
                    if (ViewModel != null)
                    {
                        var settings = App.Current.Services.GetService<SettingsService>();
                        if (settings != null)
                        {
                            var newThickness = settings.GetImageSelectionBorderThickness();
                            if (Math.Abs(ViewModel.SelectionBorderThickness - newThickness) > 0.01)
                            {
                                ViewModel.SelectionBorderThickness = newThickness;
                            }
                        }
                    }
                }
                catch (System.OperationCanceledException) { break; }
                catch { }
            }
        }

        private void ImagePage_Unloaded(object sender, RoutedEventArgs e)
        {
            try { ViewModel?.CancelLoads(); } catch { }
            try { _borderThicknessUpdateCts?.Cancel(); } catch { }
            try { _previewLoadCts?.Cancel(); } catch { }
            try { CloseImagePreview(); } catch { }
        }

        private void ImagePage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsive(e.NewSize.Width);
            UpdatePreviewSize();
        }

        private System.Threading.CancellationTokenSource? _resizeCts;

        private void UpdateResponsive(double width)
        {
            if (width <= 0) return;
            try { _resizeCts?.Cancel(); } catch { }
            _resizeCts = new System.Threading.CancellationTokenSource();
            var ct = _resizeCts.Token;
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(150, ct);
                    if (ct.IsCancellationRequested) return;
                    var columns = (int)System.Math.Max(1, System.Math.Floor((width - 48) / 220));
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (ViewModel == null) return;
                        if (columns != ViewModel.MasonryColumnCount)
                        {
                            ViewModel.MasonryColumnCount = columns;
                        }
                    });
                }
                catch { }
            });
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ImageViewModel.CurrentLayout))
            {
                if (ViewModel != null)
                {
                    ApplyLayout(ViewModel.CurrentLayout);
                }
            }
        }

        private void ApplyLayout(LayoutType layout)
        {
            switch (layout)
            {
                case LayoutType.List:
                    ItemsRepeaterMain.Layout = new StackLayout() { Orientation = Orientation.Vertical };
                    ItemsRepeaterMain.Visibility = Visibility.Visible;
                    MasonryColumnsControl.Visibility = Visibility.Collapsed;
                    break;

                case LayoutType.Grid:
                    ItemsRepeaterMain.Layout = new UniformGridLayout
                    {
                        MinItemWidth = 220,
                        MinItemHeight = 170,
                        MinRowSpacing = 8,
                        MinColumnSpacing = 8
                    };
                    ItemsRepeaterMain.Visibility = Visibility.Visible;
                    MasonryColumnsControl.Visibility = Visibility.Collapsed;
                    break;

                case LayoutType.Masonry:
                    UpdateResponsive(ActualWidth);
                    if (ViewModel != null)
                    {
                        double available = System.Math.Max(0, ActualWidth - 48);
                        int cols = ViewModel.MasonryColumnCount > 0 ? ViewModel.MasonryColumnCount : 1;
                        if (cols <= 0) cols = 1;
                        ViewModel.MasonryColumnWidth = System.Math.Floor(available / cols) - 16;
                        ViewModel.BuildMasonryColumns();
                    }
                    ItemsRepeaterMain.Visibility = Visibility.Collapsed;
                    MasonryColumnsControl.Visibility = Visibility.Visible;
                    break;

                
                default:
                    ItemsRepeaterMain.Layout = new UniformGridLayout
                    {
                        MinItemWidth = 220,
                        MinItemHeight = 170,
                        MinRowSpacing = 8,
                        MinColumnSpacing = 8
                    };
                    ItemsRepeaterMain.Visibility = Visibility.Visible;
                    MasonryColumnsControl.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void ImageItem_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement element && element.DataContext is TemplateItem item && ViewModel != null)
                {
                    var keyModifiers = Windows.System.VirtualKeyModifiers.Control;
                    var isCtrlPressed = (keyModifiers & Windows.System.VirtualKeyModifiers.Control) == Windows.System.VirtualKeyModifiers.Control;
                    keyModifiers = Windows.System.VirtualKeyModifiers.Shift;
                    var isShiftPressed = (keyModifiers & Windows.System.VirtualKeyModifiers.Shift) == Windows.System.VirtualKeyModifiers.Shift;

                    // より正確な方法: InputKeyboardSourceを使用
                    try
                    {
                        var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
                        var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
                        isCtrlPressed = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
                        isShiftPressed = (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
                    }
                    catch { }

                    _isPointerDown = true;
                    _isDragIntent = false;
                    _pointerDownPoint = e.GetCurrentPoint(element).Position;
                    _pressedItem = item;
                    _pendingSelectionForClick = false;
                    _pendingSelectionItem = null;
                    element.CapturePointer(e.Pointer);

                    if (isShiftPressed && _lastSelectedItemForRange != null)
                    {
                        var items = ViewModel.Images.ToList();
                        var startIndex = items.IndexOf(_lastSelectedItemForRange);
                        var endIndex = items.IndexOf(item);
                        if (startIndex >= 0 && endIndex >= 0)
                        {
                            var minIndex = Math.Min(startIndex, endIndex);
                            var maxIndex = Math.Max(startIndex, endIndex);
                            var rangeItems = items.Skip(minIndex).Take(maxIndex - minIndex + 1);
                            ViewModel.SelectionManager.SelectRange(rangeItems);
                        }
                    }
                    else if (isCtrlPressed)
                    {
                        ViewModel.SelectionManager.SelectItem(item, isCtrlPressed, isShiftPressed);
                        if (!isCtrlPressed)
                        {
                            _lastSelectedItemForRange = item;
                        }
                    }
                    else
                    {
                        _pendingSelectionForClick = true;
                        _pendingSelectionItem = item;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImageItem_PointerPressed error: {ex.Message}");
            }
        }

        private void ImageItem_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isPointerDown || _pressedItem == null) return;
            try
            {
                var currentPoint = e.GetCurrentPoint((UIElement)sender).Position;
                var dx = currentPoint.X - _pointerDownPoint.X;
                var dy = currentPoint.Y - _pointerDownPoint.Y;
                if (!_isDragIntent && (dx * dx + dy * dy) >= DragActivationThresholdSquared)
                {
                    _isDragIntent = true;
                }
            }
            catch { }
        }

        private void ImageItem_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_isPointerDown) return;
            _isPointerDown = false;
            if (!_isDragIntent && _pendingSelectionForClick && _pendingSelectionItem != null)
            {
                ViewModel.SelectionManager.SelectItem(_pendingSelectionItem, false, false);
                _lastSelectedItemForRange = _pendingSelectionItem;
            }
            _pendingSelectionForClick = false;
            _pendingSelectionItem = null;
            _isDragIntent = false;
            _pressedItem = null;
            if (sender is UIElement uiElement)
            {
                try { uiElement.ReleasePointerCapture(e.Pointer); } catch { }
            }
        }

        private async void ImageItem_DragStarting(UIElement sender, DragStartingEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement element && element.DataContext is TemplateItem item && ViewModel != null)
                {
                    // DragDropServiceのモジュール化されたメソッドを使用
                    var selectedItems = ViewModel.SelectionManager.SelectedItems;
                    await DragDropService.HandleDragStartingForTemplateItem(sender, e, item, selectedItems);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImageItem_DragStarting error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                e.Cancel = true; // エラー時はドラッグをキャンセル
            }
        }

        // 画像プレビュー機能
        private async void ImageItem_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement element && element.DataContext is TemplateItem item)
                {
                    await ShowImagePreview(item);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImageItem_DoubleTapped error: {ex.Message}");
            }
        }

        // 右クリックメニュー
        private void ImageItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement element && element.DataContext is TemplateItem item)
                {
                    // 共通サービスを使用してメニューを設定
                    ItemContextMenuService.SetupContextMenu(element, item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImageItem_RightTapped error: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task ShowImagePreview(TemplateItem item)
        {
            try
            {
                // 既に読み込み中の場合は、前の処理をキャンセルして閉じる
                if (_isPreviewLoading)
                {
                    try
                    {
                        _previewLoadCts?.Cancel();
                        CloseImagePreview();
                        // 少し待ってから新しいプレビューを開く
                        await System.Threading.Tasks.Task.Delay(100);
                    }
                    catch { }
                }

                if (string.IsNullOrWhiteSpace(item.Path) || !System.IO.File.Exists(item.Path))
                {
                    System.Diagnostics.Debug.WriteLine($"ShowImagePreview: File not found: {item.Path}");
                    return;
                }

                var overlay = this.FindName("ImagePreviewOverlay") as Grid;
                var imageControl = this.FindName("ImagePreviewImage") as Image;
                var scrollViewer = this.FindName("ImagePreviewScrollViewer") as ScrollViewer;

                if (overlay == null || imageControl == null || scrollViewer == null) return;

                // 読み込み開始フラグを設定
                _isPreviewLoading = true;
                _previewLoadCts = new System.Threading.CancellationTokenSource();
                var ct = _previewLoadCts.Token;

                // 前のBitmapImageをクリーンアップ
                if (_currentPreviewBitmap != null)
                {
                    try
                    {
                        _currentPreviewBitmap.ImageOpened -= OnBitmapImageOpened;
                        _currentPreviewBitmap.ImageFailed -= OnBitmapImageFailed;
                    }
                    catch { }
                    _currentPreviewBitmap = null;
                }

                // 画像を読み込む（アスペクト比を維持しながら一律のサイズで表示）
                StorageFile? imageFile = null;
                IRandomAccessStream? stream = null;
                try
                {
                    imageFile = await StorageFile.GetFileFromPathAsync(item.Path);
                    if (ct.IsCancellationRequested) return;
                    
                    stream = await imageFile.OpenReadAsync();
                    if (ct.IsCancellationRequested)
                    {
                        try { stream?.Dispose(); } catch { }
                        return;
                    }
                }
                catch (Exception fileEx)
                {
                    System.Diagnostics.Debug.WriteLine($"ShowImagePreview: Failed to open file: {fileEx.Message}");
                    _isPreviewLoading = false;
                    return;
                }
                
                if (stream == null)
                {
                    System.Diagnostics.Debug.WriteLine($"ShowImagePreview: Stream is null");
                    _isPreviewLoading = false;
                    return;
                }
                
                if (ct.IsCancellationRequested)
                {
                    try { stream?.Dispose(); } catch { }
                    _isPreviewLoading = false;
                    return;
                }
                
                var bitmapImage = new BitmapImage();
                _currentPreviewBitmap = bitmapImage;
                
                // ウィンドウサイズに基づいてデコードサイズを決定
                var windowWidth = ActualWidth > 0 ? ActualWidth : 1200;
                var windowHeight = ActualHeight > 0 ? ActualHeight : 800;
                var maxDisplayWidth = Math.Max(400, windowWidth * 0.9);
                var maxDisplayHeight = Math.Max(300, windowHeight * 0.9);
                
                // デコードサイズは表示サイズの2倍程度に設定（高解像度ディスプレイ対応）
                bitmapImage.DecodePixelWidth = (int)(maxDisplayWidth * 2);
                
                // イベントハンドラーを登録
                bitmapImage.ImageFailed += OnBitmapImageFailed;
                bitmapImage.ImageOpened += OnBitmapImageOpened;
                
                try
                {
                    await bitmapImage.SetSourceAsync(stream);
                    
                    if (ct.IsCancellationRequested)
                    {
                        _isPreviewLoading = false;
                        return;
                    }

                    // UIスレッドで実行されていることを確認
                    if (DispatcherQueue.HasThreadAccess)
                    {
                        imageControl.Source = bitmapImage;
                        
                        // ズームとスクロール位置をリセット（最初は全体表示）
                        _currentZoomFactor = 1.0;
                        if (overlay.Visibility == Visibility.Visible)
                        {
                            scrollViewer.ChangeView(0, 0, 1.0f, false);
                        }
                        
                        // プレビューサイズを更新（画像全体が表示されるように）
                        UpdatePreviewSize();

                        // オーバーレイを表示
                        overlay.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (ct.IsCancellationRequested) return;
                            
                            var overlay2 = this.FindName("ImagePreviewOverlay") as Grid;
                            var imageControl2 = this.FindName("ImagePreviewImage") as Image;
                            var scrollViewer2 = this.FindName("ImagePreviewScrollViewer") as ScrollViewer;
                            
                            if (overlay2 == null || imageControl2 == null || scrollViewer2 == null)
                            {
                                _isPreviewLoading = false;
                                return;
                            }
                            
                            imageControl2.Source = bitmapImage;
                            
                            // ズームとスクロール位置をリセット（最初は全体表示）
                            _currentZoomFactor = 1.0;
                            if (overlay2.Visibility == Visibility.Visible)
                            {
                                scrollViewer2.ChangeView(0, 0, 1.0f, false);
                            }
                            
                            // プレビューサイズを更新（画像全体が表示されるように）
                            UpdatePreviewSize();
                            
                            overlay2.Visibility = Visibility.Visible;
                        });
                    }
                    
                    _isPreviewLoading = false;
                }
                catch (Exception loadEx)
                {
                    System.Diagnostics.Debug.WriteLine($"ShowImagePreview: Failed to load image: {loadEx.Message}");
                    // ストリームをクリーンアップ
                    try { stream?.Dispose(); } catch { }
                    _isPreviewLoading = false;
                    
                    // イベントハンドラーを削除
                    try
                    {
                        bitmapImage.ImageOpened -= OnBitmapImageOpened;
                        bitmapImage.ImageFailed -= OnBitmapImageFailed;
                    }
                    catch { }
                    
                    if (_currentPreviewBitmap == bitmapImage)
                    {
                        _currentPreviewBitmap = null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowImagePreview error: {ex.Message}");
                _isPreviewLoading = false;
            }
        }

        private void OnBitmapImageFailed(object sender, ExceptionRoutedEventArgs args)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"ImagePreview: Failed to load image: {args.ErrorMessage}");
                // プレビューを閉じる
                DispatcherQueue.TryEnqueue(() =>
                {
                    CloseImagePreview();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImagePreview.ImageFailed handler error: {ex.Message}");
            }
        }

        private void OnBitmapImageOpened(object sender, RoutedEventArgs args)
        {
            try
            {
                if (sender is not BitmapImage bitmapImage) return;
                
                // UIスレッドで実行
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        var imageControl = this.FindName("ImagePreviewImage") as Image;
                        var scrollViewer = this.FindName("ImagePreviewScrollViewer") as ScrollViewer;
                        if (imageControl == null) return;
                        
                        // 画像の実際のサイズを取得
                        var actualWidth = bitmapImage.PixelWidth;
                        var actualHeight = bitmapImage.PixelHeight;
                        
                        if (actualWidth > 0 && actualHeight > 0)
                        {
                            // 画像サイズに関わらず、アスペクト比を正しく計算
                            _previewImageAspectRatio = (double)actualWidth / actualHeight;
                            
                            // プレビューサイズを更新（画像全体が表示されるように）
                            UpdatePreviewSize();
                            
                            // 画像全体が表示されるようにスクロール位置をリセット
                            // 画像サイズに関わらず、常に画像全体が表示されるようにする
                            if (scrollViewer != null && Math.Abs(_currentZoomFactor - 1.0) < 0.01)
                            {
                                scrollViewer.ChangeView(0, 0, 1.0f, false);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ImagePreview.ImageOpened handler error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImagePreview.ImageOpened dispatcher error: {ex.Message}");
            }
        }

        private void UpdatePreviewSize()
        {
            try
            {
                var overlay = this.FindName("ImagePreviewOverlay") as Grid;
                var container = this.FindName("ImagePreviewContainer") as Border;
                var imageControl = this.FindName("ImagePreviewImage") as Image;
                var scrollViewer = this.FindName("ImagePreviewScrollViewer") as ScrollViewer;
                
                if (overlay == null || container == null || imageControl == null || scrollViewer == null) return;
                
                // ウィンドウサイズに基づいて最大表示エリアを計算
                var windowWidth = ActualWidth > 0 ? ActualWidth : 1200;
                var windowHeight = ActualHeight > 0 ? ActualHeight : 800;
                
                // 最大表示エリアサイズ（マージン10%を考慮）
                var maxDisplayWidth = Math.Max(400, windowWidth * 0.8);
                var maxDisplayHeight = Math.Max(300, windowHeight * 0.8);
                
                // 画像のアスペクト比が有効な場合、画像全体が表示されるようにサイズを計算
                if (_previewImageAspectRatio > 0)
                {
                    double displayWidth, displayHeight;
                    
                    // 最大エリア内に画像全体が収まるように計算（fit to view）
                    var maxAspectRatio = maxDisplayWidth / maxDisplayHeight;
                    
                    if (_previewImageAspectRatio > maxAspectRatio)
                    {
                        // 横長の画像: 幅を基準にする
                        displayWidth = maxDisplayWidth;
                        displayHeight = maxDisplayWidth / _previewImageAspectRatio;
                    }
                    else
                    {
                        // 縦長の画像: 高さを基準にする
                        displayHeight = maxDisplayHeight;
                        displayWidth = maxDisplayHeight * _previewImageAspectRatio;
                    }
                    
                    displayWidth = Math.Min(displayWidth, maxDisplayWidth);
                    displayHeight = Math.Min(displayHeight, maxDisplayHeight);
                    
                    // 画像サイズを設定（最初は全体が表示される）
                    imageControl.Width = displayWidth;
                    imageControl.Height = displayHeight;
                    
                    // コンテナも画像サイズに合わせて比率を変える
                    container.Width = displayWidth;
                    container.Height = displayHeight;
                    container.MaxWidth = maxDisplayWidth;
                    container.MaxHeight = maxDisplayHeight;
                    
                    // ズームが1.0の場合は、画像全体が表示されるようにスクロール位置をリセット
                    if (Math.Abs(_currentZoomFactor - 1.0) < 0.01)
                    {
                        scrollViewer.ChangeView(0, 0, 1.0f, false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdatePreviewSize error: {ex.Message}");
            }
        }

        private void ImagePreviewCloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseImagePreview();
        }

        private void CloseImagePreview()
        {
            try
            {
                // 読み込み中の処理をキャンセル
                _previewLoadCts?.Cancel();
                _isPreviewLoading = false;
                
                // パンニング状態をリセット
                _isPanning = false;
                
                var overlay = this.FindName("ImagePreviewOverlay") as Grid;
                if (overlay != null)
                {
                    overlay.Visibility = Visibility.Collapsed;
                }
                
                // 画像をクリアしてメモリを解放
                var imageControl = this.FindName("ImagePreviewImage") as Image;
                if (imageControl != null)
                {
                    imageControl.Source = null;
                }
                
                // BitmapImageのイベントハンドラーをクリーンアップ
                if (_currentPreviewBitmap != null)
                {
                    try
                    {
                        _currentPreviewBitmap.ImageOpened -= OnBitmapImageOpened;
                        _currentPreviewBitmap.ImageFailed -= OnBitmapImageFailed;
                    }
                    catch { }
                    _currentPreviewBitmap = null;
                }
                
                // ズームとアスペクト比をリセット
                _currentZoomFactor = 1.0;
                _previewImageAspectRatio = 1.0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CloseImagePreview error: {ex.Message}");
            }
        }

        private void ImagePreviewOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // 背景クリックで閉じる（画像の外側、またはオーバーレイ背景をクリックした場合）
            try
            {
                var overlay = sender as FrameworkElement;
                if (overlay == null) return;

                var closeButton = this.FindName("ImagePreviewCloseButton") as FrameworkElement;
                if (closeButton != null && IsOrDescendant(e.OriginalSource as DependencyObject, closeButton))
                {
                    return;
                }

                var imageControl = this.FindName("ImagePreviewImage") as FrameworkElement;
                if (imageControl != null && IsPointInsideElement(e.GetCurrentPoint(overlay).Position, imageControl, overlay))
                {
                    return;
                }

                CloseImagePreview();
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImagePreviewOverlay_PointerPressed error: {ex.Message}");
            }
        }

        private bool IsPointInsideElement(Point point, FrameworkElement element, FrameworkElement reference)
        {
            try
            {
                if (element == null || reference == null) return false;
                if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return false;

                var transform = element.TransformToVisual(reference);
                var bounds = transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                return bounds.Contains(point);
            }
            catch
            {
                return false;
            }
        }

        private bool IsOrDescendant(DependencyObject? source, FrameworkElement target)
        {
            if (source == null || target == null) return false;
            if (source == target) return true;
            var parent = VisualTreeHelper.GetParent(source);
            while (parent != null)
            {
                if (parent == target) return true;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return false;
        }

        private bool IsDescendantOf(FrameworkElement? element, FrameworkElement ancestor)
        {
            if (element == null || ancestor == null) return false;
            if (element == ancestor) return true;
            
            var parent = element.Parent as FrameworkElement;
            while (parent != null)
            {
                if (parent == ancestor) return true;
                parent = parent.Parent as FrameworkElement;
            }
            return false;
        }

        private void ImagePreviewOverlay_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            // マウスホイールでズーム（リミッターのみ適用、ループなし）
            try
            {
                var overlay = this.FindName("ImagePreviewOverlay") as Grid;
                if (overlay == null || overlay.Visibility != Visibility.Visible) return;
                
                var scrollViewer = this.FindName("ImagePreviewScrollViewer") as ScrollViewer;
                if (scrollViewer == null) return;

                var delta = e.GetCurrentPoint(sender as UIElement).Properties.MouseWheelDelta;
                var zoomFactor = delta > 0 ? 1.1f : 0.9f;
                
                // ScrollViewerの現在のズーム値を取得
                var currentZoom = (float)scrollViewer.ZoomFactor;
                var newZoom = currentZoom * zoomFactor;
                
                // 最小値・最大値でリミッターを適用（ループなし）
                newZoom = Math.Max(0.1f, Math.Min(10.0f, newZoom));
                
                // リミットに達している場合は処理しない
                if ((delta > 0 && currentZoom >= 10.0f) || (delta < 0 && currentZoom <= 0.1f))
                {
                    e.Handled = true;
                    return;
                }
                
                // 現在のスクロール位置を保持してズーム
                scrollViewer.ChangeView(scrollViewer.HorizontalOffset, scrollViewer.VerticalOffset, newZoom, true);
                _currentZoomFactor = newZoom;
                
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImagePreviewOverlay_PointerWheelChanged error: {ex.Message}");
            }
        }

        private void ImagePreviewContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // パンニング開始
            try
            {
                if (sender is FrameworkElement element)
                {
                    _isPanning = true;
                    _lastPanPoint = e.GetCurrentPoint(element).Position;
                    element.CapturePointer(e.Pointer);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImagePreviewContainer_PointerPressed error: {ex.Message}");
            }
        }

        private void ImagePreviewContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // パンニング中
            try
            {
                if (!_isPanning || sender is not FrameworkElement element) return;

                var overlay = this.FindName("ImagePreviewOverlay") as Grid;
                if (overlay == null || overlay.Visibility != Visibility.Visible) return;

                var scrollViewer = this.FindName("ImagePreviewScrollViewer") as ScrollViewer;
                if (scrollViewer == null) return;

                var currentPoint = e.GetCurrentPoint(element).Position;
                var deltaX = currentPoint.X - _lastPanPoint.X;
                var deltaY = currentPoint.Y - _lastPanPoint.Y;

                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - deltaX);
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - deltaY);

                _lastPanPoint = currentPoint;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImagePreviewContainer_PointerMoved error: {ex.Message}");
            }
        }

        private void ImagePreviewContainer_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            // パンニング終了
            try
            {
                if (sender is FrameworkElement element)
                {
                    _isPanning = false;
                    element.ReleasePointerCapture(e.Pointer);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImagePreviewContainer_PointerReleased error: {ex.Message}");
            }
        }

        private void ImagePreviewBackground_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // ScrollViewer内の背景（画像の周囲）がクリックされた場合に閉じる
            try
            {
                var originalSource = e.OriginalSource as FrameworkElement;
                var imageControl = this.FindName("ImagePreviewImage") as FrameworkElement;
                
                // 画像がクリックされた場合は処理しない（パンニング用）
                if (imageControl != null && (originalSource == imageControl || IsDescendantOf(originalSource, imageControl)))
                {
                    return;
                }
                
                // 画像以外の場所（背景）がクリックされた場合は閉じる
                CloseImagePreview();
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImagePreviewBackground_PointerPressed error: {ex.Message}");
            }
        }
    }
}

