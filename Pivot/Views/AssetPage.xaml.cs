using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Pivot.ViewModels;
using System.ComponentModel;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Services;
using Pivot.Models;
using System;
using System.Linq;
using Microsoft.UI.Xaml.Input;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace Pivot.Views
{
    public sealed partial class AssetPage : Page
    {
        public AssetViewModel ViewModel { get; set; }

        private TemplateItem? _lastSelectedItemForRange;
        private System.Threading.CancellationTokenSource? _borderThicknessUpdateCts;

        public AssetPage()
        {
            this.InitializeComponent();
            ViewModel = new AssetViewModel();
            this.DataContext = ViewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            SizeChanged += AssetPage_SizeChanged;

            var copyAccelerator = new KeyboardAccelerator
            {
                Key = Windows.System.VirtualKey.C,
                Modifiers = Windows.System.VirtualKeyModifiers.Control
            };
            copyAccelerator.Invoked += CopySelectionKeyboardAccelerator_Invoked;
            this.KeyboardAccelerators.Add(copyAccelerator);

            ApplyLayout(ViewModel.CurrentLayout);

            try
            {
                var settings = App.Current.Services.GetService<SettingsService>();
                if (settings != null)
                {
                    var dirs = settings.GetUserSettings().AssetDirectories;
                    if (dirs != null && dirs.Count > 0)
                    {
                        _ = ViewModel.LoadFromDirectoriesAsync(dirs, 300);
                    }
                }
            }
            catch { }

            this.Unloaded += AssetPage_Unloaded;
            
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

        private void AssetPage_Unloaded(object sender, RoutedEventArgs e)
        {
            try { ViewModel?.CancelLoads(); } catch { }
            try { _borderThicknessUpdateCts?.Cancel(); } catch { }
        }

        private void AssetItem_PointerPressed(object sender, PointerRoutedEventArgs e)
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
                        var items = ViewModel.DisplayedAssets.ToList();
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
                            // 3Dファイルのみビューワーに表示（画像やその他は表示しない）
                            if (item.Kind == AssetKind.Model)
                            {
                                ViewModel.SelectedAsset = item;
                            }
                        }
                    }

                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AssetItem_PointerPressed error: {ex.Message}");
            }
        }

        private async void AssetItem_DragStarting(UIElement sender, DragStartingEventArgs e)
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
                System.Diagnostics.Debug.WriteLine($"AssetItem_DragStarting error: {ex.Message}");
                e.Cancel = true; // エラー時はドラッグをキャンセル
            }
        }

        private void AssetItem_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement element && element.Tag is string filePath && !string.IsNullOrWhiteSpace(filePath))
                {
                    if (System.IO.File.Exists(filePath))
                    {
                        // Open file with default application
                        var processInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        };
                        System.Diagnostics.Process.Start(processInfo);
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AssetItem_DoubleTapped error: {ex.Message}");
            }
        }

        // 右クリックメニュー
        private void AssetItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
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
                System.Diagnostics.Debug.WriteLine($"AssetItem_RightTapped error: {ex.Message}");
            }
        }

        private async void CopySelectionKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            await CopySelectedFilesToClipboardAsync().ConfigureAwait(false);
        }

        private async Task CopySelectedFilesToClipboardAsync()
        {
            try
            {
                if (ViewModel == null) return;

                var selectedItems = ViewModel.SelectionManager.SelectedItems;
                if (selectedItems == null || selectedItems.Count == 0) return;

                var filePaths = selectedItems
                    .Select(item => item.Path)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct()
                    .Where(File.Exists)
                    .ToList();

                if (filePaths.Count == 0) return;

                var storageItems = await DragDropService.CreateStorageItems(filePaths).ConfigureAwait(false);
                if (storageItems.Count == 0) return;

                var dataPackage = new DataPackage
                {
                    RequestedOperation = DataPackageOperation.Copy
                };
                dataPackage.SetStorageItems(storageItems, readOnly: false);

                Clipboard.SetContent(dataPackage);
                Clipboard.Flush();

                System.Diagnostics.Debug.WriteLine($"AssetPage: Copied {storageItems.Count} item(s) to clipboard");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AssetPage.CopySelectedFilesToClipboardAsync error: {ex.Message}");
            }
        }

        private void AssetPage_SizeChanged(object sender, SizeChangedEventArgs e)
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
            if (e.PropertyName == nameof(AssetViewModel.CurrentLayout))
            {
                if (ViewModel != null)
                {
                    ApplyLayout(ViewModel.CurrentLayout);
                }
            }
        }

        private void ApplyLayout(LayoutType layout)
        {
            var itemsRepeater = this.FindName("ItemsRepeaterMain") as Microsoft.UI.Xaml.Controls.ItemsRepeater;
            var masonryControl = this.FindName("MasonryColumnsControl") as Microsoft.UI.Xaml.Controls.ItemsControl;

            switch (layout)
            {
                case LayoutType.List:
                    if (itemsRepeater != null)
                    {
                        itemsRepeater.Layout = new Microsoft.UI.Xaml.Controls.StackLayout() { Orientation = Orientation.Vertical };
                        itemsRepeater.Visibility = Visibility.Visible;
                    }
                    if (masonryControl != null) masonryControl.Visibility = Visibility.Collapsed;
                    break;

                case LayoutType.Grid:
                    if (itemsRepeater != null)
                    {
                        itemsRepeater.Layout = new Microsoft.UI.Xaml.Controls.UniformGridLayout
                        {
                            MinItemWidth = 220,
                            MinItemHeight = 170,
                            MinRowSpacing = 8,
                            MinColumnSpacing = 8
                        };
                        itemsRepeater.Visibility = Visibility.Visible;
                    }
                    if (masonryControl != null) masonryControl.Visibility = Visibility.Collapsed;
                    break;

                case LayoutType.Masonry:
                    UpdateResponsive(ActualWidth);
                    if (ViewModel != null)
                    {
                        double available = System.Math.Max(0, ActualWidth - 48);
                        int cols = ViewModel.MasonryColumnCount > 0 ? ViewModel.MasonryColumnCount : 1;
                        if (cols <= 0) cols = 1;
                        ViewModel.MasonryColumnWidth = System.Math.Floor(available / cols) - 16;
                    }
                    // build masonry columns from currently displayed (filtered) assets so layout change preserves filtering
                    if (ViewModel != null)
                    {
                        ViewModel.BuildMasonryColumns(ViewModel.DisplayedAssets);
                    }
                    if (itemsRepeater != null) itemsRepeater.Visibility = Visibility.Collapsed;
                    if (masonryControl != null) masonryControl.Visibility = Visibility.Visible;
                    break;

                
                default:
                    if (itemsRepeater != null)
                    {
                        itemsRepeater.Layout = new Microsoft.UI.Xaml.Controls.UniformGridLayout
                        {
                            MinItemWidth = 220,
                            MinItemHeight = 170,
                            MinRowSpacing = 8,
                            MinColumnSpacing = 8
                        };
                        itemsRepeater.Visibility = Visibility.Visible;
                    }
                    if (masonryControl != null) masonryControl.Visibility = Visibility.Collapsed;
                    break;
            }
        }
    }
}
