using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Controls;
using Pivot.Collections;
using Pivot.Models;
using Pivot.Messages;
using Pivot.Services;
using Pivot.ViewModels;
using Pivot.Utilities;

namespace Pivot.Controls
{
    /// <summary>
    /// Unified browser control for displaying assets with virtualized scrolling.
    /// Supports Masonry, Grid, and List layouts with filtering.
    /// </summary>
    public sealed partial class UnifiedBrowserControl : UserControl, 
        IRecipient<BulkItemsChangedMessage<AssetEntity>>,
        IRecipient<DirectoryRemovedMessage>
    {
        private BrowserViewModel? _viewModel;
        private PreloadedAssetCollection? _collection;
        private LayoutBasedTemplateSelector? _templateSelector;
        private CancellationTokenSource? _searchDebounceToken;
        private bool _sortAscending = false;
        private IBitmapCacheService? _bitmapCache;

        /// <summary>
        /// The target asset kind to display (Image, Code, etc).
        /// </summary>
        public AssetKind? TargetKind { get; set; }

        // Interaction Events
        public event EventHandler<AssetEntity>? ItemClicked;
        public event EventHandler<AssetEntity>? ItemDoubleClicked;
        public event EventHandler<(AssetEntity Asset, Windows.Foundation.Point Position)>? ItemRightTapped;

        public UnifiedBrowserControl()
        {
            this.InitializeComponent();
            _templateSelector = Resources["LayoutTemplateSelector"] as LayoutBasedTemplateSelector;
            
            // Register for real-time asset updates
            WeakReferenceMessenger.Default.Register<BulkItemsChangedMessage<AssetEntity>>(this);
            WeakReferenceMessenger.Default.Register<DirectoryRemovedMessage>(this);
            this.Unloaded += (s, e) => WeakReferenceMessenger.Default.UnregisterAll(this);
            
            // Register scroll event for incremental loading (ItemsRepeater doesn't support ISupportIncrementalLoading)
            ContentScrollViewer.ViewChanged += ContentScrollViewer_ViewChanged;
        }

        /// <summary>
        /// Initialize the browser with the specified asset kind.
        /// </summary>
        public async Task InitializeAsync(AssetKind? kind = null)
        {
            TargetKind = kind;

            // Get ViewModel from DI
            _viewModel = App.Current.Services.GetRequiredService<BrowserViewModel>();
            _viewModel.SetTargetKind(kind);
            _viewModel.CriteriaChanged += OnCriteriaChanged;

            // Get BitmapCacheService for image recycling
            _bitmapCache = App.Current.Services.GetRequiredService<IBitmapCacheService>();

            // Create preloaded collection for instant filtering
            _collection = new PreloadedAssetCollection();
            _collection.LoadingStateChanged += OnLoadingStateChanged;
            _collection.CountsUpdated += OnCountsUpdated;

            AssetRepeater.ItemsSource = _collection;

            // Load initial batch for instant display (prevents UI freeze)
            await _collection.LoadInitialBatchAsync(kind ?? AssetKind.Image, App.Current.Services);

            UpdateLayout(_viewModel.CurrentLayout);
            UpdateEmptyState();
        }

        /// <summary>
        /// Handle scroll events (no longer needed - ScrollView handles virtualization automatically).
        /// </summary>
        private void ContentScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            // No-op: ScrollView + ItemsRepeater handles virtualization automatically
        }

        #region Criteria Change Handlers

        private void OnCriteriaChanged(object? sender, EventArgs e)
        {
            if (_viewModel == null || _collection == null) return;

            System.Diagnostics.Debug.WriteLine($"[UnifiedBrowser] OnCriteriaChanged - Directory: '{_viewModel.FilterCriteria.Directory ?? "(null)"}'");
            
            // INSTANT client-side filter - no database query!
            _collection.ApplyFilter(_viewModel.FilterCriteria);
            UpdateEmptyState();
        }

        private void OnLoadingStateChanged(object? sender, bool isLoading)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                LoadingRing.IsActive = isLoading;
                FilterBarLoadingIndicator.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
                if (_viewModel != null)
                {
                    _viewModel.IsLoading = isLoading;
                }
                
                // When loading completes, invalidate MasonryLayout to recalculate with correct aspect ratios
                // This fixes the issue where images appear squished on initial load because
                // AspectRatio data wasn't available when the layout was first calculated.
                if (!isLoading && AssetRepeater.Layout is MasonryLayout masonryLayout)
                {
                    masonryLayout.Invalidate();
                }
            });
        }

        private void OnCountsUpdated(object? sender, (int Loaded, int Total) counts)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _viewModel?.UpdateCounts(counts.Loaded, counts.Total);
                UpdateStatusText();
                UpdateEmptyState();
            });
        }

        #endregion

        #region FilterBar Handlers

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Debounce search to avoid excessive queries
            _searchDebounceToken?.Cancel();
            _searchDebounceToken = new CancellationTokenSource();
            var token = _searchDebounceToken.Token;

            _ = Task.Delay(300, token).ContinueWith(t =>
            {
                if (!t.IsCanceled && _viewModel != null)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        _viewModel.SetSearchQuery(SearchBox.Text);
                    });
                }
            });
        }

        private void RatingFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel == null || RatingFilter.SelectedItem is not ComboBoxItem item) return;

            if (int.TryParse(item.Tag?.ToString(), out int rating))
            {
                _viewModel.SetMinRating(rating);
            }
        }

        private void SortField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel == null || SortFieldCombo.SelectedItem is not ComboBoxItem item) return;

            var fieldStr = item.Tag?.ToString();
            if (Enum.TryParse<SortField>(fieldStr, out var field))
            {
                var direction = _sortAscending ? SortDirection.Ascending : SortDirection.Descending;
                _viewModel.SetSorting(field, direction);
            }
        }

        private void SortDirection_Click(object sender, RoutedEventArgs e)
        {
            _sortAscending = !_sortAscending;
            SortDirectionIcon.Glyph = _sortAscending ? "\uE74A" : "\uE74B"; // Up/Down arrows

            if (_viewModel != null && SortFieldCombo.SelectedItem is ComboBoxItem item)
            {
                var fieldStr = item.Tag?.ToString();
                if (Enum.TryParse<SortField>(fieldStr, out var field))
                {
                    var direction = _sortAscending ? SortDirection.Ascending : SortDirection.Descending;
                    _viewModel.SetSorting(field, direction);
                }
            }
        }

        #endregion

        #region Item Interaction Handlers (Parent-Level)

        // Helper to find the AssetEntity from a tapped element
        // x:Bind を使用している DataTemplate 内では DataContext が設定されないため、
        // ItemsRepeater.GetElementIndex() を使用してインデックスからデータを取得する
        private AssetEntity? FindAssetFromElement(DependencyObject? element)
        {
            // まず視覚ツリーを辿ってItemsRepeaterの直接の子要素を見つける
            DependencyObject? current = element;
            while (current != null)
            {
                var parent = VisualTreeHelper.GetParent(current);
                if (parent == AssetRepeater)
                {
                    // currentがItemsRepeaterの直接の子要素
                    if (current is UIElement uiElement)
                    {
                        int index = AssetRepeater.GetElementIndex(uiElement);
                        if (index >= 0 && _collection != null && index < _collection.Count)
                        {
                            return _collection[index];
                        }
                    }
                    break;
                }
                
                // DataContext でも試す（フォールバック）
                if (current is FrameworkElement fe && fe.DataContext is AssetEntity asset)
                {
                    return asset;
                }
                
                current = parent;
            }
            return null;
        }

        private void AssetRepeater_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var asset = FindAssetFromElement(e.OriginalSource as DependencyObject);
            if (asset == null || _collection == null) return;
            
            // Detect modifier keys
            bool isCtrl = false;
            bool isShift = false;
            try
            {
                var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
                var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
                isCtrl = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
                isShift = (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
            }
            catch { }
            
            System.Diagnostics.Debug.WriteLine($"[Tapped] {asset.FileName} Ctrl={isCtrl} Shift={isShift}");
            
            if (isShift && _lastClickedAsset != null)
            {
                // Shift+Click: Range selection
                var startIndex = _collection.IndexOf(_lastClickedAsset);
                var endIndex = _collection.IndexOf(asset);
                
                if (startIndex >= 0 && endIndex >= 0)
                {
                    int low = Math.Min(startIndex, endIndex);
                    int high = Math.Max(startIndex, endIndex);
                    
                    // Clear existing selection first (unless Ctrl is also held - standard explorer behavior usually extends)
                    // For simplicity, sticking to: Shift without Ctrl = Range select (exclusive)
                    if (!isCtrl) 
                    {
                        foreach (var item in _selectedAssets) item.IsSelected = false;
                        _selectedAssets.Clear();
                    }
                    
                    // Select range
                    for (int i = low; i <= high && i < _collection.Count; i++)
                    {
                        var rangeAsset = _collection[i];
                        if (!rangeAsset.IsSelected)
                        {
                            rangeAsset.IsSelected = true;
                            _selectedAssets.Add(rangeAsset);
                        }
                    }
                }
            }
            else if (isCtrl)
            {
                // Ctrl+Click: Toggle selection
                if (asset.IsSelected)
                {
                    asset.IsSelected = false;
                    _selectedAssets.Remove(asset);
                }
                else
                {
                    asset.IsSelected = true;
                    _selectedAssets.Add(asset);
                }
                _lastClickedAsset = asset;
            }
            else
            {
                // Normal click: Single selection (clear others)
                // Use HashSet for fast clear
                foreach (var item in _selectedAssets) item.IsSelected = false;
                _selectedAssets.Clear();

                asset.IsSelected = true;
                _selectedAssets.Add(asset);
                _lastClickedAsset = asset;
            }
            
            ItemClicked?.Invoke(this, asset);
        }

        private void AssetRepeater_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var asset = FindAssetFromElement(e.OriginalSource as DependencyObject);
            if (asset != null)
            {
                System.Diagnostics.Debug.WriteLine($"[DoubleTapped] {asset.FileName}");
                ItemDoubleClicked?.Invoke(this, asset);
            }
        }

        private void AssetRepeater_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var asset = FindAssetFromElement(e.OriginalSource as DependencyObject);
            if (asset != null)
            {
                var position = e.GetPosition(this);
                System.Diagnostics.Debug.WriteLine($"[RightTapped] {asset.FileName}");
                ItemRightTapped?.Invoke(this, (asset, position));
            }
        }

        // Multi-selection state
        private readonly HashSet<AssetEntity> _selectedAssets = new();
        private AssetEntity? _lastClickedAsset; // For Shift-click range selection
        
        /// <summary>
        /// Gets the currently selected assets.
        /// </summary>
        public IReadOnlyCollection<AssetEntity> SelectedItems => _selectedAssets;

        /// <summary>
        /// Moves the selection by the specified delta (e.g., +1 for next, -1 for previous).
        /// Returns the newly selected asset, or null if no change.
        /// </summary>
        public AssetEntity? MoveSelection(int delta)
        {
            if (_collection == null || _collection.Count == 0) return null;

            int currentIndex = -1;
            
            // If multiple items selected, use the last clicked one as anchor
            if (_lastClickedAsset != null)
            {
                currentIndex = _collection.IndexOf(_lastClickedAsset);
            }
            else if (_selectedAssets.Count > 0)
            {
                // Fallback to first selected
                currentIndex = _collection.IndexOf(_selectedAssets.First());
            }

            if (currentIndex == -1)
            {
                // No selection, start from beginning if attempting to move next
                currentIndex = delta > 0 ? -1 : 0;
            }

            int newIndex = Math.Clamp(currentIndex + delta, 0, _collection.Count - 1);
            if (newIndex == currentIndex) return null; // End of list

            var newAsset = _collection[newIndex];
            
            // Update selection state
            foreach (var item in _selectedAssets) item.IsSelected = false;
            _selectedAssets.Clear();

            newAsset.IsSelected = true;
            _selectedAssets.Add(newAsset);
            _lastClickedAsset = newAsset;
            
            // Ensure visible
            AssetRepeater.GetOrCreateElement(newIndex).StartBringIntoView();

            return newAsset;
        }

        private void ClearSelectionVisuals()
        {
             foreach (var item in _selectedAssets) item.IsSelected = false;
             _selectedAssets.Clear();
             _lastClickedAsset = null;
        }
        
        private async void ItemBorder_DragStarting(UIElement sender, DragStartingEventArgs e)
        {
            // ★重要: 非同期処理を待ってもらうためにDeferralを取得
            var deferral = e.GetDeferral();

            try
            {
                var asset = FindAssetFromElement(sender);
                if (asset == null)
                {
                    System.Diagnostics.Debug.WriteLine("[DragStarting] No asset found");
                    e.Cancel = true;
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"[DragStarting] {asset.FileName}");
                
                // If dragged item is not in selection, select it alone
                if (!asset.IsSelected)
                {
                    ClearSelectionVisuals();
                    asset.IsSelected = true;
                    _selectedAssets.Add(asset);
                }
                
                // Use DragDropService for AssetEntity
                await DragDropService.HandleDragStartingForAssetEntity(sender, e, asset, _selectedAssets);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DragStarting] Error: {ex.Message}");
                e.Cancel = true;
            }
            finally
            {
                // ★重要: 処理が終わったら必ず完了を通知
                deferral.Complete();
            }
        }
        
        
        private async void AssetRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if (_bitmapCache == null) return;
            if (args.Element is not FrameworkElement element) return;
            if (element.DataContext is not AssetEntity asset) return;

            try
            {
                // Find the Image control in the element tree
                var image = TreeHelper.FindDescendantOfType<Image>(element);
                if (image == null) return;

                // Remember the asset for validation after async call
                var originalAsset = asset;
                var imagePath = asset.DisplayImageSource;

                // Get cached or load new BitmapImage
                var bitmap = await _bitmapCache.GetOrLoadAsync(imagePath, 200);

                // CRITICAL: Validate DataContext hasn't changed during async operation
                // This prevents race condition where fast scrolling causes wrong images to appear
                if (element.DataContext != originalAsset)
                {
                    System.Diagnostics.Debug.WriteLine($"[ElementPrepared] DataContext changed, discarding image for {imagePath}");
                    return; // Element is now showing a different item, discard this result
                }

                // DataContext is still the same, safe to set image
                image.Source = bitmap;
                image.Opacity = 0; // Start transparent for fade-in
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ElementPrepared] Error loading image: {ex.Message}");
            }
        }
        
        private void AssetRepeater_ElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
        {
            // No-op: BitmapCacheService handles memory management via LRU
            // Images remain in cache for instant reuse when scrolling back
        }


        private void Image_ImageOpened(object sender, RoutedEventArgs e)
        {
            // Fade within animation 
            if (sender is Image img)
            {
                img.Opacity = 1;
            }
        }

        #endregion

        #region Layout Management

        private void UpdateLayout(LayoutType layoutType)
        {
            if (_templateSelector != null)
            {
                _templateSelector.CurrentLayout = layoutType;
            }

            // Update ItemsRepeater layout
            AssetRepeater.Layout = layoutType switch
            {
                LayoutType.Masonry => new MasonryLayout
                {
                    ColumnWidth = 220,
                    ColumnSpacing = 8,
                    RowSpacing = 8,
                    FooterHeight = 32
                },
                LayoutType.Grid => new UniformGridLayout
                {
                    MinItemWidth = 200,
                    MinItemHeight = 200,
                    MinRowSpacing = 4,
                    MinColumnSpacing = 4,
                    ItemsStretch = UniformGridLayoutItemsStretch.Fill
                },
                LayoutType.List => new StackLayout
                {
                    Spacing = 2,
                    Orientation = Orientation.Vertical
                },
                _ => AssetRepeater.Layout
            };

            // Update layout icon
            LayoutIcon.Glyph = layoutType switch
            {
                LayoutType.Masonry => "\uF0E2", // Grid view
                LayoutType.Grid => "\uE80A",   // Grid
                LayoutType.List => "\uE8FD",   // List
                _ => "\uE80A"
            };
            
            // Note: No ItemsSource reset - causes thumbnail flickering
        }
        
        /// <summary>
        /// Set directory filter to show only assets from a specific folder.
        /// Pass null to clear the filter and show all assets.
        /// </summary>
        public void SetDirectoryFilter(string? directoryPath)
        {
            if (_viewModel == null) return;
            _viewModel.SetDirectoryFilter(directoryPath);
        }

        private void LayoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            _viewModel.CycleLayoutCommand.Execute(null);
            UpdateLayout(_viewModel.CurrentLayout);
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || _collection == null) return;

            // Reload initial batch from database
            await _collection.LoadInitialBatchAsync(TargetKind ?? AssetKind.Image, App.Current.Services);
        }

        #endregion

        #region UI State Management

        private void UpdateStatusText()
        {
            if (_viewModel != null)
            {
                StatusText.Text = _viewModel.StatusText;
            }
        }

        private void UpdateEmptyState()
        {
            bool isEmpty = _collection == null || _collection.Count == 0;
            bool isLoading = _viewModel?.IsLoading ?? false;

            EmptyState.Visibility = isEmpty && !isLoading ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region DirectoryRemovedMessage Handler

        public void Receive(DirectoryRemovedMessage message)
        {
            // Refresh data when a directory is removed
            if (_viewModel == null || _collection == null) return;

            DispatcherQueue.TryEnqueue(async () =>
            {
                System.Diagnostics.Debug.WriteLine($"[UnifiedBrowser] Directory removed: {message.Value}, refreshing...");
                await _collection.LoadInitialBatchAsync(TargetKind ?? AssetKind.Image, App.Current.Services);
                UpdateEmptyState();
            });
        }

        #endregion

        #region BulkItemsChangedMessage Handler

        public void Receive(BulkItemsChangedMessage<AssetEntity> message)
        {
            if (_viewModel == null || _collection == null) return;

            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    var criteria = _viewModel.FilterCriteria;
                    
                    foreach (var change in message.Value)
                    {
                        if (change.Item == null) continue;
                        
                        // Check if item matches current filter criteria
                        if (!MatchesCriteria(change.Item, criteria))
                        {
                            continue;
                        }
                        
                        if (change.Type == ItemChangeData<AssetEntity>.ChangeType.Added)
                        {
                            // Add to preloaded collection (automatically filters if needed)
                            _collection.AddAsset(change.Item);
                            System.Diagnostics.Debug.WriteLine($"[UnifiedBrowser] Added: {change.Item.FileName}");
                        }
                        else if (change.Type == ItemChangeData<AssetEntity>.ChangeType.Updated)
                        {
                            // Update existing asset
                            _collection.UpdateAsset(change.Item);
                            System.Diagnostics.Debug.WriteLine($"[UnifiedBrowser] Updated: {change.Item.FileName}");
                        }
                        else if (change.Type == ItemChangeData<AssetEntity>.ChangeType.Deleted)
                        {
                            // Remove from collection
                            _collection.RemoveAsset(change.Item.FilePath);
                            System.Diagnostics.Debug.WriteLine($"[UnifiedBrowser] Removed: {change.Item.FileName}");
                        }
                    }
                    
                    UpdateStatusText();
                    UpdateEmptyState();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UnifiedBrowser] Receive error: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Check if an asset matches the current filter criteria.
        /// </summary>
        private bool MatchesCriteria(AssetEntity asset, FilterCriteria criteria)
        {
            // Check Kind
            if (criteria.TargetKind.HasValue && asset.Kind != criteria.TargetKind.Value)
            {
                return false;
            }

            // Check Directory filter
            if (!string.IsNullOrEmpty(criteria.Directory))
            {
                var assetDir = System.IO.Path.GetDirectoryName(asset.FilePath)?.Replace('/', '\\').TrimEnd('\\');
                var filterDir = criteria.Directory.Replace('/', '\\').TrimEnd('\\');
                
                if (!string.Equals(assetDir, filterDir, StringComparison.OrdinalIgnoreCase) &&
                    !(assetDir?.StartsWith(filterDir + "\\", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    return false;
                }
            }

            // Check Search text
            if (!string.IsNullOrEmpty(criteria.SearchQuery))
            {
                var searchLower = criteria.SearchQuery.ToLowerInvariant();
                if (!(asset.FileName?.ToLowerInvariant().Contains(searchLower) ?? false))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }

}

