using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Pivot.Engine.Models;
using Pivot.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Pivot.Controls
{
    public sealed partial class ImagePreviewControl : UserControl
    {
        private bool _isPreviewLoading = false;
        private CancellationTokenSource? _previewLoadCts;
        private BitmapImage? _currentPreviewBitmap;
        private double _previewImageAspectRatio = 1.0;
        private double _currentZoomFactor = 1.0;
        private bool _isPanning = false;
        private Windows.Foundation.Point _lastMousePosition;
        private bool _isPatternPreviewEnabled = false;

        // サイズ設定: 設定から読み込み、または既定値を使用
        private double _fixedDisplaySize = 800.0;       // 初期表示時の最大サイズ (幅または高さ)
        private double _maxZoom = 50.0;                 // 最大ズーム倍率

        public bool IsOpen => ImagePreviewOverlay.Visibility == Visibility.Visible;

        public ImagePreviewControl()
        {
            this.InitializeComponent();
            this.SizeChanged += ImagePreviewControl_SizeChanged;
            ImagePreviewScrollViewer.ViewChanged += ImagePreviewScrollViewer_ViewChanged;
            
            // Load settings
            try
            {
                var themeSettings = App.Current.Services.GetService(typeof(Services.ThemeSettingsService)) as Services.ThemeSettingsService;
                if (themeSettings != null)
                {
                    _fixedDisplaySize = themeSettings.PreviewFixedDisplaySize;
                    _maxZoom = themeSettings.PreviewMaxZoom;
                }
            }
            catch { }
        }

        private void PatternPreviewToggle_Click(object sender, RoutedEventArgs e)
        {
            _isPatternPreviewEnabled = PatternPreviewToggle.IsChecked == true;
            UpdatePatternPreview();
        }

        private void UpdatePatternPreview()
        {
            if (_isPatternPreviewEnabled && _currentPreviewBitmap != null)
            {
                // Show pattern grid, hide single image
                ImagePreviewImage.Visibility = Visibility.Collapsed;
                PatternGrid.Visibility = Visibility.Visible;
                
                // Assign source to all 9 images
                P00.Source = _currentPreviewBitmap; P01.Source = _currentPreviewBitmap; P02.Source = _currentPreviewBitmap;
                P10.Source = _currentPreviewBitmap; P11.Source = _currentPreviewBitmap; P12.Source = _currentPreviewBitmap;
                P20.Source = _currentPreviewBitmap; P21.Source = _currentPreviewBitmap; P22.Source = _currentPreviewBitmap;

                // Zoom out to show the pattern (fit to screen roughly)
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        var viewWidth = ImagePreviewScrollViewer.ViewportWidth;
                        var viewHeight = ImagePreviewScrollViewer.ViewportHeight;
                        
                        // Calculate zoom to fit 3x3 grid (approx)
                        // Total width = 3 * imageWidth
                        var totalW = _currentPreviewBitmap.PixelWidth * 3;
                        var totalH = _currentPreviewBitmap.PixelHeight * 3;
                        
                        if (totalW > 0 && totalH > 0)
                        {
                            var zoomX = viewWidth / totalW;
                            var zoomY = viewHeight / totalH;
                            var zoom = Math.Min(zoomX, zoomY);
                            // Cap max zoom at 1.0 (pixel perfect) but allow shrinking
                            if (zoom > 1.0) zoom = 1.0f; 
                            
                            // Apply slightly loosely
                            ImagePreviewScrollViewer.ChangeView(null, null, (float)zoom, false);
                        }
                    }
                    catch { }
                });
            }
            else
            {
                // Show single image, hide pattern grid
                ImagePreviewImage.Visibility = Visibility.Visible;
                PatternGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void ImagePreviewScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pointerPoint = e.GetCurrentPoint(sender as UIElement);
            if (pointerPoint.Properties.IsMiddleButtonPressed)
            {
                _isPanning = true;
                _lastMousePosition = pointerPoint.Position;
                (sender as UIElement)?.CapturePointer(e.Pointer);
                
                // Optional: Change cursor to Hand/Fist effectively if possible, but minimal implementation first
                // ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
                
                e.Handled = true;
            }
        }

        private void ImagePreviewScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isPanning)
            {
                var pointerPoint = e.GetCurrentPoint(sender as UIElement);
                var currentPosition = pointerPoint.Position;
                var deltaX = currentPosition.X - _lastMousePosition.X;
                var deltaY = currentPosition.Y - _lastMousePosition.Y;

                var sv = ImagePreviewScrollViewer;
                if (sv != null)
                {
                    sv.ChangeView(sv.HorizontalOffset - deltaX, sv.VerticalOffset - deltaY, null, true);
                }

                _lastMousePosition = currentPosition;
                e.Handled = true;
            }
        }

        private void ImagePreviewScrollViewer_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isPanning)
            {
                var pointerPoint = e.GetCurrentPoint(sender as UIElement);
                if (!pointerPoint.Properties.IsMiddleButtonPressed)
                {
                    _isPanning = false;
                    (sender as UIElement)?.ReleasePointerCapture(e.Pointer);
                    e.Handled = true;
                }
            }
        }

        private void ImagePreviewScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var pointerPoint = e.GetCurrentPoint(ImagePreviewScrollViewer);
            var delta = pointerPoint.Properties.MouseWheelDelta;

            if (delta == 0) return;

            // Calculate new zoom factor
            var scrollViewer = ImagePreviewScrollViewer;
            var currentZoom = scrollViewer.ZoomFactor;
            
            // Zoom speed: 10% per tick (120) roughly
            const double zoomRatio = 1.1;
            var newZoom = delta > 0 ? currentZoom * zoomRatio : currentZoom / zoomRatio;

            // Clamp values
            if (newZoom < scrollViewer.MinZoomFactor) newZoom = scrollViewer.MinZoomFactor;
            if (newZoom > scrollViewer.MaxZoomFactor) newZoom = scrollViewer.MaxZoomFactor;

            if (Math.Abs(newZoom - currentZoom) < 0.001) return;

            // Calculate new offsets to keep cursor centered
            var mousePos = pointerPoint.Position;
            
            // Logic:
            // contentX = (horizontalOffset + mouseX) / currentZoom
            // newHorizontalOffset = contentX * newZoom - mouseX
            
            var horizontalOffset = scrollViewer.HorizontalOffset;
            var verticalOffset = scrollViewer.VerticalOffset;

            var contentX = (horizontalOffset + mousePos.X) / currentZoom;
            var contentY = (verticalOffset + mousePos.Y) / currentZoom;

            var newHorizontalOffset = contentX * newZoom - mousePos.X;
            var newVerticalOffset = contentY * newZoom - mousePos.Y;
            
            scrollViewer.ChangeView(newHorizontalOffset, newVerticalOffset, (float)newZoom, true);
            
            e.Handled = true;
        }

        private void ImagePreviewScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            _currentZoomFactor = ImagePreviewScrollViewer.ZoomFactor;
        }

        private void ImagePreviewControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePreviewSize();
        }

        public async Task ShowAsync(TemplateItem item)
        {
            if (_isPreviewLoading)
            {
                try
                {
                    _previewLoadCts?.Cancel();
                    Close();
                    await Task.Delay(100);
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(item.Path) || !System.IO.File.Exists(item.Path))
            {
                System.Diagnostics.Debug.WriteLine($"ShowAsync: File not found: {item.Path}");
                return;
            }

            _isPreviewLoading = true;
            _previewLoadCts = new CancellationTokenSource();
            var ct = _previewLoadCts.Token;

            CleanupCurrentBitmap();

            StorageFile? imageFile = null;
            IRandomAccessStream? stream = null;

            try
            {
                imageFile = await StorageFile.GetFileFromPathAsync(item.Path);
                if (ct.IsCancellationRequested) return;

                stream = await imageFile.OpenReadAsync();
                if (ct.IsCancellationRequested)
                {
                    stream?.Dispose();
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowAsync: Failed to open file: {ex.Message}");
                _isPreviewLoading = false;
                return;
            }

            if (stream == null)
            {
                _isPreviewLoading = false;
                return;
            }

            if (ct.IsCancellationRequested)
            {
                stream?.Dispose();
                _isPreviewLoading = false;
                return;
            }

            var bitmapImage = new BitmapImage();
            _currentPreviewBitmap = bitmapImage;

            var windowWidth = ActualWidth > 0 ? ActualWidth : 1200;
            var maxDisplayWidth = Math.Max(400, windowWidth * 0.9);
            bitmapImage.DecodePixelWidth = (int)(maxDisplayWidth * 2);

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

                ImagePreviewImage.Source = bitmapImage;
                _currentZoomFactor = 1.0;
                
                if (ImagePreviewOverlay.Visibility == Visibility.Visible)
                {
                   // ズームをリセットせずにUpdatePreviewSizeに任せる。
                   // ただし初期状態であることを示すために0にしておく手もあるが、
                   // ChangeViewはUpdatePreviewSizeで呼ばれる
                   _currentZoomFactor = 0; // Force update in UpdatePreviewSize
                }

                ImagePreviewOverlay.Visibility = Visibility.Visible;
                UpdatePreviewSize();
                _isPreviewLoading = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowAsync: Failed to load image: {ex.Message}");
                stream?.Dispose();
                _isPreviewLoading = false;
                CleanupCurrentBitmap();
            }
        }

        private void ImagePreviewImage_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            ToggleZoom();
        }

        private void ToggleZoom()
        {
            if (_currentPreviewBitmap == null) return;

            var actualW = _currentPreviewBitmap.PixelWidth;
            var actualH = _currentPreviewBitmap.PixelHeight;
            if (actualW == 0 || actualH == 0) return;

            var windowWidth = ActualWidth > 0 ? ActualWidth : 1200;
            var windowHeight = ActualHeight > 0 ? ActualHeight : 800;

            // 固定表示サイズを使用
            var availableWidth = Math.Min(windowWidth * 0.9, _fixedDisplaySize);
            var availableHeight = Math.Min(windowHeight * 0.9, _fixedDisplaySize);
            
            var wRatio = availableWidth / actualW;
            var hRatio = availableHeight / actualH;
            var fitZoom = Math.Min(wRatio, hRatio);
            if (fitZoom > 1.0) fitZoom = 1.0;
            fitZoom = Math.Max(fitZoom, 0.1);

            // Toggle between Fit and 1.0 (or Max possible if 1.0 is huge?)
            // If we are close to Fit, go to 1.0. Otherwise go to Fit.
            
            var currentZoom = _currentZoomFactor;
            const double threshold = 0.01;

            if (Math.Abs(currentZoom - fitZoom) < threshold)
            {
                // Go to 100%
                ImagePreviewScrollViewer.ChangeView(null, null, 1.0f, false);
            }
            else
            {
                // Go to Fit
                ImagePreviewScrollViewer.ChangeView(null, null, (float)fitZoom, false);
            }
        }

        public void Close()
        {
            try
            {
                _previewLoadCts?.Cancel();
                _isPreviewLoading = false;
                ImagePreviewOverlay.Visibility = Visibility.Collapsed;
                ImagePreviewImage.Source = null;
                CleanupCurrentBitmap();
                _currentZoomFactor = 1.0;
                _previewImageAspectRatio = 0; // 0でリセット
                
                // Reset pattern preview
                _isPatternPreviewEnabled = false;
                PatternPreviewToggle.IsChecked = false;
                PatternPreviewToggle.IsChecked = false;
                PatternGrid.Visibility = Visibility.Collapsed;
                ImagePreviewImage.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Close error: {ex.Message}");
            }
        }

        private void CleanupCurrentBitmap()
        {
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
        }

        private void OnBitmapImageOpened(object sender, RoutedEventArgs e)
        {
             if (sender is not BitmapImage bitmapImage) return;
             
             DispatcherQueue.TryEnqueue(() =>
             {
                 var actualWidth = bitmapImage.PixelWidth;
                 var actualHeight = bitmapImage.PixelHeight;

                 if (actualWidth > 0 && actualHeight > 0)
                 {
                      // アスペクト比だけでなく、フィット処理を行う
                      UpdatePreviewSize();
                  }
             });
        }

        private void OnBitmapImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Image load failed: {e.ErrorMessage}");
            DispatcherQueue.TryEnqueue(() => Close());
        }

        private void UpdatePreviewSize()
        {
            if (ImagePreviewOverlay.Visibility != Visibility.Visible) return;
            if (_currentPreviewBitmap == null) return;
            
            var actualW = _currentPreviewBitmap.PixelWidth;
            var actualH = _currentPreviewBitmap.PixelHeight;
            if (actualW == 0 || actualH == 0) return;

            // 固定表示領域サイズ（すべての画像で一律）
            var windowWidth = ActualWidth > 0 ? ActualWidth : 1200;
            var windowHeight = ActualHeight > 0 ? ActualHeight : 800;
            
            // 表示領域: ウィンドウから固定マージンを引いたサイズ（一律）
            var fixedViewportWidth = windowWidth - 60;   // 左右30pxずつ
            var fixedViewportHeight = windowHeight - 100; // 上60px、下40px
            
            // フィット倍率の計算 (画像全体が表示されるように)
            var wRatio = fixedViewportWidth / actualW;
            var hRatio = fixedViewportHeight / actualH;
            var fitZoom = Math.Min(wRatio, hRatio);
            
            // 小さい画像は拡大しない (最大1.0倍)
            if (fitZoom > 1.0) fitZoom = 1.0;
            
            // MinZoomFactorを先に設定（fitZoomより小さい場合に備えて）
            var minZoom = Math.Min(fitZoom, 0.1);
            ImagePreviewScrollViewer.MinZoomFactor = (float)minZoom;
            
            // 最大ズーム倍率を設定から取得
            var maxZoom = _maxZoom;
            maxZoom = Math.Max(maxZoom, 1.0);
            ImagePreviewScrollViewer.MaxZoomFactor = (float)maxZoom;
            
            // 画像のサイズを設定（元のピクセルサイズ）
            ImagePreviewImage.Width = actualW;
            ImagePreviewImage.Height = actualH;

            // レイアウト更新を強制
            ImagePreviewScrollViewer.UpdateLayout();
            
            // ズームを適用（非同期で確実に適用、位置はScrollViewerの中央配置に任せる）
            var zoomToApply = (float)fitZoom;
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    // スクロール位置はnullでScrollViewerの中央配置に任せる
                    ImagePreviewScrollViewer.ChangeView(null, null, zoomToApply, true);
                }
                catch { }
            });
            
            _previewImageAspectRatio = fitZoom;
        }

        private void ImagePreviewCloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ImagePreviewOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var overlay = sender as FrameworkElement;
            if (overlay == null) return;

            // 閉じるボタン自体のクリックは無視（ボタンのClickイベントで処理）
            if (IsOrDescendant(e.OriginalSource as DependencyObject, ImagePreviewCloseButton)) return;
            
            // ツールバー（PatternPreviewToggle等）のクリックは無視
            if (IsOrDescendant(e.OriginalSource as DependencyObject, PatternPreviewToggle)) return;
            
            var clickPos = e.GetCurrentPoint(overlay).Position;
            
            // ズームアウト時は画像の実際の表示サイズを計算してチェック
            // ImagePreviewContainerではなく、実際のコンテンツ領域をチェック
            if (IsPointInsideZoomedContent(clickPos, overlay)) return;

            Close();
            e.Handled = true;
        }

        /// <summary>
        /// ズームを考慮して、実際に表示されているコンテンツ領域内かどうか判定
        /// </summary>
        private bool IsPointInsideZoomedContent(Windows.Foundation.Point clickPoint, FrameworkElement overlay)
        {
            try
            {
                // Get the current zoom factor
                var zoomFactor = ImagePreviewScrollViewer.ZoomFactor;
                
                // Get the visible content element (single image or pattern grid)
                FrameworkElement? contentElement = _isPatternPreviewEnabled && PatternGrid.Visibility == Visibility.Visible
                    ? PatternGrid
                    : ImagePreviewImage;

                if (contentElement == null) return false;
                
                // Get actual content size (before zoom)
                var contentWidth = contentElement.ActualWidth;
                var contentHeight = contentElement.ActualHeight;
                
                if (contentWidth <= 0 || contentHeight <= 0) return false;
                
                // Calculate zoomed size
                var zoomedWidth = contentWidth * zoomFactor;
                var zoomedHeight = contentHeight * zoomFactor;
                
                // Calculate content position within overlay
                // The ScrollViewer centers content, so we need to find the actual displayed bounds
                var scrollViewerTransform = ImagePreviewScrollViewer.TransformToVisual(overlay);
                var scrollViewerBounds = scrollViewerTransform.TransformBounds(
                    new Windows.Foundation.Rect(0, 0, ImagePreviewScrollViewer.ActualWidth, ImagePreviewScrollViewer.ActualHeight));
                
                // Calculate the visible content bounds within the ScrollViewer
                // When zoomed out, content is centered within ScrollViewer
                var viewportWidth = ImagePreviewScrollViewer.ViewportWidth;
                var viewportHeight = ImagePreviewScrollViewer.ViewportHeight;
                
                // Content is centered when smaller than viewport
                var contentLeftInViewport = Math.Max(0, (viewportWidth - zoomedWidth) / 2);
                var contentTopInViewport = Math.Max(0, (viewportHeight - zoomedHeight) / 2);
                
                // Adjust for scroll position
                contentLeftInViewport -= ImagePreviewScrollViewer.HorizontalOffset;
                contentTopInViewport -= ImagePreviewScrollViewer.VerticalOffset;
                
                // Translate to overlay coordinates
                var contentLeft = scrollViewerBounds.X + contentLeftInViewport;
                var contentTop = scrollViewerBounds.Y + contentTopInViewport;
                
                var contentBounds = new Windows.Foundation.Rect(
                    contentLeft, 
                    contentTop, 
                    Math.Min(zoomedWidth, viewportWidth), 
                    Math.Min(zoomedHeight, viewportHeight));
                
                return contentBounds.Contains(clickPoint);
            }
            catch 
            { 
                // Fallback: use container bounds
                return IsPointInsideElement(clickPoint, ImagePreviewContainer, overlay);
            }
        }

        private bool IsPointInsideElement(Windows.Foundation.Point point, FrameworkElement element, FrameworkElement reference)
        {
            try
            {
                if (element == null || reference == null) return false;
                if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return false;

                var transform = element.TransformToVisual(reference);
                var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));
                return bounds.Contains(point);
            }
            catch { return false; }
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
    }
}
