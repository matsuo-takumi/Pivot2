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
using Pivot.Controls;

namespace Pivot.Views
{
    public sealed partial class ImagePage : Page, IRecipient<SettingsChangedMessage>
    {
        public ImageViewModel ViewModel { get; set; }

        private MasonryLayout _masonryLayout;
        private DataTemplate _defaultItemTemplate;

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
            
            _masonryLayout = new MasonryLayout 
            { 
                ColumnWidth = 220, 
                ColumnSpacing = 8, 
                RowSpacing = 8 
            };
            
            
            // Capture default template (Grid/List)
            _defaultItemTemplate = ItemsRepeaterMain.ItemTemplate as DataTemplate;

            ViewModel = new ImageViewModel();
            this.DataContext = ViewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.FolderTree.CollectionChanged += FolderTree_CollectionChanged;

            // responsive handlers
            SizeChanged += ImagePage_SizeChanged;

            // 初期レイアウトを適用
            ApplyLayout(ViewModel.CurrentLayout);

            // 自動ロード
            try
            {
                var settings = App.Current.Services.GetService<SettingsService>();
                var dirSettings = App.Current.Services.GetService<DirectorySettingsService>();
                if (dirSettings != null)
                {
                    // Use ImageDirectories from DirectorySettingsService
                    if (dirSettings.ImageDirectories != null && dirSettings.ImageDirectories.Count > 0)
                    {
                         _ = ViewModel.LoadFromDirectoriesAsync(dirSettings.ImageDirectories);
                    }
                }
                else if (settings != null)
                {
                    // Fallback
                    var dirs = settings.GetUserSettings().ImageDirectories;
                    if (dirs != null && dirs.Count > 0)
                    {
                        _ = ViewModel.LoadFromDirectoriesAsync(dirs);
                    }
                }
            }
            catch { }

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
                    catch { }
                });
            }
        }



        private void FolderTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            if (args.InvokedItem is FolderNode node)
            {
                ViewModel.SelectedFolder = node;
            }
        }

        private void ImagePage_Unloaded(object sender, RoutedEventArgs e)
        {
            try { ViewModel.FolderTree.CollectionChanged -= FolderTree_CollectionChanged; } catch { }
            try { ViewModel?.CancelLoads(); } catch { }
            // Clean up message registration
            try { WeakReferenceMessenger.Default.UnregisterAll(this); } catch { }

            try { PreviewControl.Close(); } catch { }
        }

        private void ImagePage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsive(e.NewSize.Width);
        }

        private void UpdateResponsive(double width)
        {
             if (ViewModel?.CurrentLayout == LayoutType.Masonry)
             {
                 _masonryLayout.Invalidate();
             }
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
            else if (e.PropertyName == nameof(ImageViewModel.IsSidebarOpen))
            {
                // Re-calculate layout when sidebar toggles (especially for Masonry)
                if (ViewModel != null && ViewModel.CurrentLayout == LayoutType.Masonry)
                {
                    UpdateResponsive(ActualWidth);
                }
            }
        }

        private void ApplyLayout(LayoutType layout)
        {
            if (ItemsRepeaterMain == null) return;
            
            // Ensure Repeater is visible
            ItemsRepeaterMain.Visibility = Visibility.Visible;

            switch (layout)
            {
                case LayoutType.List:
                    ItemsRepeaterMain.Layout = new StackLayout() { Orientation = Orientation.Vertical };
                    ItemsRepeaterMain.ItemTemplate = _defaultItemTemplate;
                    break;

                case LayoutType.Masonry:
                    ItemsRepeaterMain.Layout = _masonryLayout;
                    if (Resources.TryGetValue("MasonryItemTemplate", out var t))
                    {
                        ItemsRepeaterMain.ItemTemplate = t as DataTemplate;
                    }
                    UpdateResponsive(ActualWidth);
                    break;

                case LayoutType.Grid:
                default:
                    ItemsRepeaterMain.Layout = new UniformGridLayout
                    {
                        MinItemWidth = 220,
                        MinItemHeight = 170,
                        MinRowSpacing = 8,
                        MinColumnSpacing = 8
                    };
                    ItemsRepeaterMain.ItemTemplate = _defaultItemTemplate;
                    break;
            }
        }

        // ... Existing Pointer Handlers ...

        private void FolderTree_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() => SyncFolderTree());
        }

        private void SyncFolderTree()
        {
            try
            {
                FolderTreeView.RootNodes.Clear();
                foreach (var node in ViewModel.FolderTree)
                {
                    var tvNode = CreateTreeViewNode(node);
                    FolderTreeView.RootNodes.Add(tvNode);
                }
            }
            catch { }
        }

        private TreeViewNode CreateTreeViewNode(FolderNode node)
        {
            // Update: Manually create UI to ensure text visibility
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Padding = new Microsoft.UI.Xaml.Thickness(0, 4, 0, 4) };
            
            // Icon
            var icon = new FontIcon 
            { 
                Glyph = "\uE8B7", 
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), 
                FontSize = 16 
            };
            // Try to get Accent color, fallback to default
            if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var accentColor))
            {
               // resource is usually Color, need Brush
               icon.Foreground = new SolidColorBrush((Windows.UI.Color)accentColor);
            }
            
            stack.Children.Add(icon);

            // Text
            var textBlock = new TextBlock 
            { 
                Text = node.Name, 
                VerticalAlignment = VerticalAlignment.Center 
            };
            stack.Children.Add(textBlock);

            var tvNode = new TreeViewNode() { Content = stack, IsExpanded = node.IsExpanded };
            
            foreach (var child in node.Children)
            {
                tvNode.Children.Add(CreateTreeViewNode(child));
            }
            return tvNode;
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
            catch { }
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

