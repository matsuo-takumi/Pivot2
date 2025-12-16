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

        public ImagePreviewControl()
        {
            this.InitializeComponent();
            this.InitializeComponent();
            this.SizeChanged += ImagePreviewControl_SizeChanged;
            ImagePreviewScrollViewer.ViewChanged += ImagePreviewScrollViewer_ViewChanged;
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

                UpdatePreviewSize();
                ImagePreviewOverlay.Visibility = Visibility.Visible;
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

            // マージンを考慮した最大表示エリア (画面の90%)
            var maxDisplayWidth = Math.Max(400, windowWidth * 0.9);
            var maxDisplayHeight = Math.Max(300, windowHeight * 0.9);
            
            // フィット倍率の計算
            var wRatio = maxDisplayWidth / actualW;
            var hRatio = maxDisplayHeight / actualH;
            var fitZoom = Math.Min(wRatio, hRatio);
             
            // 小さい画像は拡大しない (最大1.0倍)
            if (fitZoom > 1.0) fitZoom = 1.0;
            
            // ScrollViewerのMinZoomFactorを下回らないようにする
            fitZoom = Math.Max(fitZoom, 0.1);

            var displayW = actualW * fitZoom;
            var displayH = actualH * fitZoom;

            // コンテナサイズをフィットサイズに設定 (余白がクリックに反応しないように)
            ImagePreviewContainer.Width = displayW;
            ImagePreviewContainer.Height = displayH;
            
            // 画像自体は本来のピクセルサイズを設定し、ScrollViewerのズーム機能に任せる
            ImagePreviewImage.Width = actualW;
            ImagePreviewImage.Height = actualH;

            // 現在「フィット表示」に近い状態、または初期表示なら、新しいフィット倍率を適用
            // _currentZoomFactorは初期値1.0だが、ShowAsyncでリセットされる
            bool isNearFit = Math.Abs(_previewImageAspectRatio - fitZoom) < 0.001 || _currentZoomFactor == 0;
            
            // 簡易的に、リサイズ時は常にフィットさせる（ユーザー体験として一般的）
            // ただしユーザーが拡大中なら維持したいが、コンテナサイズが変わると維持も難しい
            ImagePreviewScrollViewer.ChangeView(null, null, (float)fitZoom, true);
            
            // 次回の比較用にフィット倍率を保存しておく必要があればフィールドに追加するが、
            // ここでは簡易実装として再適用する
        }

        private void ImagePreviewCloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ImagePreviewOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var overlay = sender as FrameworkElement;
            if (overlay == null) return;

            if (IsOrDescendant(e.OriginalSource as DependencyObject, ImagePreviewCloseButton)) return;
            if (IsPointInsideElement(e.GetCurrentPoint(overlay).Position, ImagePreviewImage, overlay)) return;

            Close();
            e.Handled = true;
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
