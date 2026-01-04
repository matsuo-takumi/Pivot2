using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
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

        public bool IsOpen => ImagePreviewOverlay.Visibility == Visibility.Visible;

        public ImagePreviewControl()
        {
            this.InitializeComponent();
            this.SizeChanged += ImagePreviewControl_SizeChanged;
            ImagePreviewScrollViewer.ViewChanged += ImagePreviewScrollViewer_ViewChanged;
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

            var maxDisplayWidth = Math.Max(400, windowWidth * 0.9);
            var maxDisplayHeight = Math.Max(300, windowHeight * 0.9);
            
            var wRatio = maxDisplayWidth / actualW;
            var hRatio = maxDisplayHeight / actualH;
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

            var windowWidth = ActualWidth > 0 ? ActualWidth : 1200;
            var windowHeight = ActualHeight > 0 ? ActualHeight : 800;

            // マージンを考慮した最大表示エリア (画面の98%: ほとんどいっぱいまで表示)
            var maxDisplayWidth = Math.Max(400, windowWidth * 0.98);
            var maxDisplayHeight = Math.Max(300, windowHeight * 0.98);
            
            // フィット倍率の計算
            var wRatio = maxDisplayWidth / actualW;
            var hRatio = maxDisplayHeight / actualH;
            var fitZoom = Math.Min(wRatio, hRatio);
             
            // 小さい画像は拡大しない (最大1.0倍)
            if (fitZoom > 1.0) fitZoom = 1.0;
            
            // ScrollViewerのMinZoomFactorを、フィット倍率が0.1未満の場合にも対応できるように更新
            // fitZoomが極端に小さい場合 (例: 0.05)、MinZoomFactorが0.1だとフィットしないため
            if (fitZoom < ImagePreviewScrollViewer.MinZoomFactor)
            {
                ImagePreviewScrollViewer.MinZoomFactor = (float)fitZoom;
            }

            var displayW = actualW * fitZoom;
            var displayH = actualH * fitZoom;

            // コンテナサイズをフィットサイズに設定 (余白がクリックに反応しないように)
            ImagePreviewContainer.Width = displayW;
            ImagePreviewContainer.Height = displayH;
            
            // 画像自体は本来のピクセルサイズを設定し、ScrollViewerのズーム機能に任せる
            ImagePreviewImage.Width = actualW;
            ImagePreviewImage.Height = actualH;

            // レイアウト更新後にChangeViewを呼ぶことで確実に適用させる
            ImagePreviewScrollViewer.UpdateLayout();

            // 現在「フィット表示」に近い状態、または初期表示なら、新しいフィット倍率を適用
            // _currentZoomFactorは初期値1.0だが、ShowAsyncでリセットされる
            bool isNearFit = Math.Abs(_previewImageAspectRatio - fitZoom) < 0.001 || _currentZoomFactor == 0;
            
            // 簡易的に、リサイズ時は常にフィットさせる（ユーザー体験として一般的）
            // ただしユーザーが拡大中なら維持したいが、コンテナサイズが変わると維持も難しい
            if (!ImagePreviewScrollViewer.ChangeView(null, null, (float)fitZoom, true))
            {
                 // ChangeViewが失敗した場合のリトライ（稀にある）または同期呼び出し的な処理が必要か検討
                 // ここでは念のためDisableAnimationでもう一度試行
                 ImagePreviewScrollViewer.ChangeView(null, null, (float)fitZoom, true);
            }
            
            // 次回の比較用にフィット倍率を保存
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
