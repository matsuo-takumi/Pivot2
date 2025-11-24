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

                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImageItem_PointerPressed error: {ex.Message}");
            }
        }

        private async void ImageItem_DragStarting(UIElement sender, DragStartingEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement element && element.DataContext is TemplateItem item && ViewModel != null)
                {
                    // 選択されているアイテムを取得（選択されていない場合は現在のアイテムのみ）
                    var selectedItems = ViewModel.SelectionManager.SelectedItems;
                    var itemsToDrag = selectedItems.Count > 0 ? selectedItems.Cast<object>() : new[] { (object)item };

                    // DragDropServiceを使用してドラッグを開始
                    var filePaths = itemsToDrag.Cast<TemplateItem>().Select(i => i.Path).Where(path => !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path)).ToList();
                    if (filePaths.Count == 0) return;

                    var storageItems = await DragDropService.CreateStorageItems(filePaths);
                    if (!storageItems.Any()) return;

                    // StorageItemsを設定（外部アプリケーションやフォルダへのドロップをサポート）
                    e.Data.SetStorageItems(storageItems);
                    
                    // コピー操作を要求
                    e.Data.RequestedOperation = DataPackageOperation.Copy;
                    
                    // ドラッグUIの設定（システムが自動的にファイルアイコンを表示）
                    e.DragUI.SetContentFromDataPackage();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImageItem_DragStarting error: {ex.Message}");
                e.Cancel = true; // エラー時はドラッグをキャンセル
            }
        }
    }
}
