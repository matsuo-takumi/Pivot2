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

namespace Pivot.Views
{
    public sealed partial class ImagePage : Page
    {
        public ImageViewModel ViewModel { get; set; }

        private TemplateItem? _lastSelectedItemForRange;
        private System.Threading.CancellationTokenSource? _borderThicknessUpdateCts;

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
        }

        private void ImagePage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsive(e.NewSize.Width);
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

                    // 範囲選択の開始点を設定
                    if (isShiftPressed && _lastSelectedItemForRange != null && item != null)
                    {
                        // Shift+クリック: 範囲選択
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
                    else if (item != null)
                    {
                        // 通常クリックまたはCtrl+クリック
                        ViewModel.SelectionManager.SelectItem(item, isCtrlPressed, isShiftPressed);
                        if (!isCtrlPressed)
                        {
                            _lastSelectedItemForRange = item;
                        }
                    }

                    // 注意: e.Handled = true を設定しないことで、ドラッグが正常に開始される
                    // ただし、これにより他のイベントも処理される可能性がある
                    // ドラッグを開始するためには、この行をコメントアウトする必要がある
                    // e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImageItem_PointerPressed error: {ex.Message}");
            }
        }

        private async void ImageItem_DragStarting(UIElement sender, DragStartingEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("ImageItem_DragStarting: Event fired!");
            try
            {
                if (sender is FrameworkElement element && element.DataContext is TemplateItem item && ViewModel != null)
                {
                    System.Diagnostics.Debug.WriteLine($"ImageItem_DragStarting: Item found - Name: {item.Name}, Path: {item.Path}");
                    // 選択されているアイテムを取得（選択されていない場合は現在のアイテムのみ）
                    var selectedItems = ViewModel.SelectionManager.SelectedItems;
                    var itemsToDrag = selectedItems.Count > 0 ? selectedItems.Cast<object>() : new[] { (object)item };

                    // DragDropServiceを使用してドラッグを開始
                    var filePaths = itemsToDrag.Cast<TemplateItem>().Select(i => i.Path).Where(path => !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path)).ToList();
                    if (filePaths.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("ImageItem_DragStarting: No valid file paths found");
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"ImageItem_DragStarting: Attempting to drag {filePaths.Count} file(s)");
                    foreach (var path in filePaths)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - File: {path}");
                    }

                    var storageItems = await DragDropService.CreateStorageItems(filePaths);
                    if (storageItems.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("ImageItem_DragStarting: Failed to create storage items");
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"ImageItem_DragStarting: Created {storageItems.Count} storage item(s)");

                    // StorageItemsを設定（WinUI3では、readOnlyパラメータはオプション）
                    // ファイルをコピー可能にするため、readOnly: falseを指定
                    e.Data.SetStorageItems(storageItems, readOnly: false);
                    System.Diagnostics.Debug.WriteLine($"ImageItem_DragStarting: SetStorageItems called with {storageItems.Count} item(s), readOnly=false");
                    
                    // コピー操作を要求
                    e.Data.RequestedOperation = DataPackageOperation.Copy;
                    System.Diagnostics.Debug.WriteLine($"ImageItem_DragStarting: RequestedOperation set to {e.Data.RequestedOperation}");
                    
                    // カスタムドラッグプレビュー: 画像のサムネイルを表示
                    try
                    {
                        // 最初のアイテムのサムネイルを使用（複数選択時も最初の画像を表示）
                        var firstItem = itemsToDrag.Cast<TemplateItem>().FirstOrDefault();
                        if (firstItem != null)
                        {
                            BitmapImage? bitmapImage = null;
                            
                            // ThumbnailPathから画像を読み込む
                            if (!string.IsNullOrWhiteSpace(firstItem.ThumbnailPath))
                            {
                                try
                                {
                                    // URI形式の場合（file:/// や http:// など）
                                    if (Uri.TryCreate(firstItem.ThumbnailPath, UriKind.Absolute, out var thumbnailUri))
                                    {
                                        bitmapImage = new BitmapImage(thumbnailUri);
                                        bitmapImage.DecodePixelWidth = 200; // ドラッグプレビュー用にサイズを制限
                                        System.Diagnostics.Debug.WriteLine($"ImageItem_DragStarting: Created BitmapImage from URI: {firstItem.ThumbnailPath}");
                                    }
                                    // ファイルパスの場合
                                    else if (System.IO.File.Exists(firstItem.ThumbnailPath))
                                    {
                                        var thumbnailFile = await StorageFile.GetFileFromPathAsync(firstItem.ThumbnailPath);
                                        var stream = await thumbnailFile.OpenReadAsync();
                                        bitmapImage = new BitmapImage();
                                        bitmapImage.DecodePixelWidth = 200;
                                        await bitmapImage.SetSourceAsync(stream);
                                        System.Diagnostics.Debug.WriteLine($"ImageItem_DragStarting: Created BitmapImage from file: {firstItem.ThumbnailPath}");
                                    }
                                }
                                catch (Exception thumbEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"ImageItem_DragStarting: Failed to load thumbnail: {thumbEx.Message}");
                                }
                            }
                            
                            // サムネイルが利用できない場合は、元の画像ファイルから読み込む
                            if (bitmapImage == null && System.IO.File.Exists(firstItem.Path))
                            {
                                try
                                {
                                    var imageFile = await StorageFile.GetFileFromPathAsync(firstItem.Path);
                                    var stream = await imageFile.OpenReadAsync();
                                    bitmapImage = new BitmapImage();
                                    bitmapImage.DecodePixelWidth = 200; // ドラッグプレビュー用にサイズを制限
                                    await bitmapImage.SetSourceAsync(stream);
                                    System.Diagnostics.Debug.WriteLine($"ImageItem_DragStarting: Created BitmapImage from original image: {firstItem.Path}");
                                }
                                catch (Exception imgEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"ImageItem_DragStarting: Failed to load original image: {imgEx.Message}");
                                }
                            }
                            
                            // BitmapImageが作成できた場合は、カスタムプレビューを設定
                            if (bitmapImage != null)
                            {
                                e.DragUI.SetContentFromBitmapImage(bitmapImage);
                                System.Diagnostics.Debug.WriteLine("ImageItem_DragStarting: Set custom drag preview with image");
                            }
                            else
                            {
                                // フォールバック: システムデフォルトを使用
                                e.DragUI.SetContentFromDataPackage();
                                System.Diagnostics.Debug.WriteLine("ImageItem_DragStarting: Using default drag preview (fallback)");
                            }
                        }
                        else
                        {
                            // フォールバック: システムデフォルトを使用
                            e.DragUI.SetContentFromDataPackage();
                            System.Diagnostics.Debug.WriteLine("ImageItem_DragStarting: Using default drag preview (no item found)");
                        }
                    }
                    catch (Exception previewEx)
                    {
                        // プレビュー設定に失敗した場合は、システムデフォルトを使用
                        System.Diagnostics.Debug.WriteLine($"ImageItem_DragStarting: Failed to set custom preview: {previewEx.Message}");
                        e.DragUI.SetContentFromDataPackage();
                    }
                    
                    System.Diagnostics.Debug.WriteLine("ImageItem_DragStarting: Drag started successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImageItem_DragStarting error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                e.Cancel = true; // エラー時はドラッグをキャンセル
            }
        }
    }
}
