using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Messages;
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
    public sealed partial class ImagePage : Page, IRecipient<SettingsChangedMessage>
    {
        public ImageViewModel ViewModel { get; set; }

        private TemplateItem? _lastSelectedItemForRange;

        // Layout constants
        private const double ItemWidth = 220.0;
        private const double ItemHeight = 170.0;
        private const double ItemRowSpacing = 8.0;
        private const double ItemColumnSpacing = 8.0;
        private const double PagePadding = 48.0; // 12*2 (Page) + 6*2 (Border) + 6*2 (Margin/Padding approx)

        private const double DragActivationThresholdSquared = 16.0;
        private bool _isPointerDown = false;
        private bool _isDragIntent = false;
        private Windows.Foundation.Point _pointerDownPoint;
        private TemplateItem? _pressedItem;
        private bool _pendingSelectionForClick = false;
        private TemplateItem? _pendingSelectionItem;
        

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


            this.Unloaded += ImagePage_Unloaded;
            

            // Register for messages
            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this);
        }

        public void Receive(SettingsChangedMessage message)
        {
            if (message.Value == "ImageSelectionBorderThickness" && ViewModel != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
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
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ImagePage SettingsChanged Error: {ex.Message}");
                    }
                });
            }
        }
        


        private void ImagePage_Unloaded(object sender, RoutedEventArgs e)
        {
            try { ViewModel?.CancelLoads(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"ImagePage Unload Error 1: {ex.Message}"); }
            // Clean up message registration
            try { WeakReferenceMessenger.Default.UnregisterAll(this); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"ImagePage Unload Error 2: {ex.Message}"); }

            try { PreviewControl.Close(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"ImagePage Unload Error 3: {ex.Message}"); }
        }

        private void ImagePage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsive(e.NewSize.Width);
        }

        private System.Threading.CancellationTokenSource? _resizeCts;

        private void UpdateResponsive(double width)
        {
            if (width <= 0) return;
            try { _resizeCts?.Cancel(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"UpdateResponsive Cancel Error: {ex.Message}"); }
            _resizeCts = new System.Threading.CancellationTokenSource();
            var ct = _resizeCts.Token;
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(150, ct);
                    if (ct.IsCancellationRequested) return;
                    var columns = (int)System.Math.Max(1, System.Math.Floor((width - PagePadding) / ItemWidth));
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (ViewModel == null) return;
                        if (columns != ViewModel.MasonryColumnCount)
                        {
                            ViewModel.MasonryColumnCount = columns;
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"UpdateResponsive Error: {ex.Message}");
                }
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
                        MinItemWidth = ItemWidth,
                        MinItemHeight = ItemHeight,
                        MinRowSpacing = ItemRowSpacing,
                        MinColumnSpacing = ItemColumnSpacing
                    };
                    ItemsRepeaterMain.Visibility = Visibility.Visible;
                    MasonryColumnsControl.Visibility = Visibility.Collapsed;
                    break;

                case LayoutType.Masonry:
                    UpdateResponsive(ActualWidth);
                    if (ViewModel != null)
                    {
                        double available = System.Math.Max(0, ActualWidth - PagePadding);
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
                        MinItemWidth = ItemWidth,
                        MinItemHeight = ItemHeight,
                        MinRowSpacing = ItemRowSpacing,
                        MinColumnSpacing = ItemColumnSpacing
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
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ImageItem_PointerPressed KeyState Error: {ex.Message}");
                    }

                    _isPointerDown = true;
                    _isDragIntent = false;
                    _pointerDownPoint = e.GetCurrentPoint(element).Position;
                    _pressedItem = item;
                    _pendingSelectionForClick = false;
                    _pendingSelectionItem = null;
                    element.CapturePointer(e.Pointer);

                    element.CapturePointer(e.Pointer);

                    if (isShiftPressed || isCtrlPressed)
                    {
                        // 修飾キーがある場合は即座に処理（ドラッグ開始前）
                        ViewModel.HandleSelection(item, isCtrlPressed, isShiftPressed);
                        _pendingSelectionForClick = false;
                    }
                    else
                    {
                        // 修飾キーがない場合は、クリック確定（PointerReleased）まで待つ
                        // ドラッグ操作の可能性があるため
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImageItem_PointerMoved Error: {ex.Message}");
            }
        }

        private void ImageItem_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_isPointerDown) return;
            _isPointerDown = false;
            if (!_isDragIntent && _pendingSelectionForClick && _pendingSelectionItem != null)
            {
                // ドラッグせずに離した場合はクリックとみなす（単一選択）
                ViewModel.HandleSelection(_pendingSelectionItem, false, false);
            }
            _pendingSelectionForClick = false;
            _pendingSelectionItem = null;
            _isDragIntent = false;
            _pressedItem = null;
            if (sender is UIElement uiElement)
            {
                try { uiElement.ReleasePointerCapture(e.Pointer); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"ImageItem_PointerReleased Capture Error: {ex.Message}"); }
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
                    await PreviewControl.ShowAsync(item);
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


    }
}

