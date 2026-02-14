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
using Pivot.Engine.Models;
using Pivot.Models;
using Pivot.Messages;
using Pivot.Engine.Messages;
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
        private IncrementalAssetCollection? _collection;
        private LayoutBasedTemplateSelector? _templateSelector;
        private CancellationTokenSource? _searchDebounceToken;
        private bool _sortAscending = false;

        /// <summary>
        /// The target asset kind to display (Image, Code, etc).
        /// </summary>
        public AssetKind? TargetKind { get; set; }

        // Interaction Events
        public event EventHandler<AssetModel>? ItemClicked;
        public event EventHandler<AssetModel>? ItemDoubleClicked;
        public event EventHandler<(AssetModel Asset, Windows.Foundation.Point Position)>? ItemRightTapped;

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

            // Create collection
            _collection = new IncrementalAssetCollection(
                App.Current.Services, 
                _viewModel.FilterCriteria);

            _collection.LoadingStateChanged += OnLoadingStateChanged;
            _collection.CountsUpdated += OnCountsUpdated;

            AssetRepeater.ItemsSource = _collection;

            // Initial load
            await _collection.ResetAsync(_viewModel.FilterCriteria);

            UpdateLayout(_viewModel.CurrentLayout);
            UpdateEmptyState();
        }

        /// <summary>
        /// Handle scroll events to trigger incremental loading.
        /// </summary>
        private async void ContentScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_collection == null || !_collection.HasMoreItems) return;
            
            // Calculate distance from bottom
            var scrollableHeight = ContentScrollViewer.ScrollableHeight;
            var verticalOffset = ContentScrollViewer.VerticalOffset;
            var distanceFromBottom = scrollableHeight - verticalOffset;
            
            // Load more when within 300px of bottom
            if (distanceFromBottom < 300)
            {
                await _collection.LoadMoreItemsAsync(50);
            }
        }

        private async void OnCriteriaChanged(object? sender, EventArgs e)
        {
            if (_collection != null && _viewModel != null)
            {
                await _collection.ResetAsync(_viewModel.FilterCriteria);
                UpdateEmptyState();
            }
        }

        private void OnLoadingStateChanged(object? sender, bool isLoading)
        {
            if (_viewModel != null)
            {
                _viewModel.IsLoading = isLoading;
            }
            UpdateEmptyState();
        }

        private void OnCountsUpdated(object? sender, (int Loaded, int Total) e)
        {
             UpdateStatusText();
        }

        // FilterBar Handlers

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.SetSearchQuery(SearchBox.Text);
            }
        }

        private void RatingFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel != null && RatingFilter.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out int rating))
            {
                _viewModel.SetMinRating(rating);
            }
        }

        private void SortField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel != null && SortFieldCombo.SelectedItem is ComboBoxItem item && item.Tag is string fieldName)
            {
                if (Enum.TryParse<Pivot.Services.SortField>(fieldName, out var field))
                {
                    _viewModel.SetSorting(field, _viewModel.FilterCriteria.SortDirection);
                }
            }
        }

        private void SortDirection_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                var newDirection = _viewModel.FilterCriteria.SortDirection == Pivot.Services.SortDirection.Ascending
                    ? Pivot.Services.SortDirection.Descending
                    : Pivot.Services.SortDirection.Ascending;
                _viewModel.SetSorting(_viewModel.FilterCriteria.SortField, newDirection);
                
                // Visual update (icon) is handled by binding or manually if needed
                SortDirectionIcon.Glyph = newDirection == Pivot.Services.SortDirection.Ascending ? "\uE74B" : "\uE74A";
            }
        }

        private void FilterColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            // Optional
        }

        private void ApplyColorFilter_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                var color = FilterColorPicker.Color;
                _viewModel.SetColorFilter(color.R, color.G, color.B);
            }
            ColorFilterButton.Flyout?.Hide();
        }

        private void ClearColorFilter_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.ClearColorFilter();
            }
            ColorFilterButton.Flyout?.Hide();
        }


        #region Item Interaction Handlers (Parent-Level)

        // Helper to find the AssetModel from a tapped element
        // x:Bind を使用している DataTemplate 内では DataContext が設定されないため、
        // ItemsRepeater.GetElementIndex() を使用してインデックスからデータを取得する
        private AssetModel? FindAssetFromElement(DependencyObject? element)
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
                if (current is FrameworkElement fe && fe.DataContext is AssetModel asset)
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
        private readonly HashSet<AssetModel> _selectedAssets = new();
        private AssetModel? _lastClickedAsset; // For Shift-click range selection
        
        /// <summary>
        /// Gets the currently selected assets.
        /// </summary>
        public IReadOnlyCollection<AssetModel> SelectedItems => _selectedAssets;

        /// <summary>
        /// Moves the selection by the specified delta (e.g., +1 for next, -1 for previous).
        /// Returns the newly selected asset, or null if no change.
        /// </summary>
        public AssetModel? MoveSelection(int delta)
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
                
                // Use DragDropService for AssetEntity (Unwrap Entity)
                // Assuming DragDropService handles IEnumerable<AssetEntity> or similar
                // We'll map AssetModels back to Entities for drag service
                var selectedEntities = _selectedAssets.Select(a => a.Entity).ToList();
                await DragDropService.HandleDragStartingForAssetEntity(sender, e, asset.Entity, selectedEntities);
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
        
        private void AssetRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
             // No manual visual sync needed - Data Binding handles it
        }
        
        private void AssetRepeater_ElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
        {
            // Fix for ghost images: Clear the UriSource when the element is recycled.
            // ItemsRepeater reuses the visual element but updates the DataContext.
            // If the new image takes time to load, the Image control keeps showing the OLD image
            // from the previous DataContext. Explicitly clearing it here ensures a blank state
            // until the new image is ready.
            var image = TreeHelper.FindDescendantOfType<Image>(args.Element);
            if (image != null)
            {
                // Reset opacity for next fade-in
                image.Opacity = 0;
                
                if (image.Source is BitmapImage bitmapImage)
                {
                    bitmapImage.UriSource = null;
                }
            }
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

        /// <summary>
        /// Apply a complete set of filter criteria (e.g., from a Smart Folder).
        /// </summary>
        public void SetFilterCriteria(FilterCriteria criteria)
        {
            if (_viewModel == null) return;
            _viewModel.SetCriteria(criteria);
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

            await _collection.ResetAsync(_viewModel.FilterCriteria);
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
                await _collection.ResetAsync(_viewModel.FilterCriteria);
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
                        
                        if (change.Type == ItemChangeData<AssetEntity>.ChangeType.Added ||
                            change.Type == ItemChangeData<AssetEntity>.ChangeType.Updated)
                        {
                            // Check if already exists by checking the underlying entity path in AssetModels
                            var existing = _collection.FirstOrDefault(a => a.FilePath == change.Item.FilePath);
                            if (existing == null)
                            {
                                // Add new item at the beginning for immediate visibility
                                // Wrap Entity in Model
                                _collection.Insert(0, AssetMapper.ToAssetModel(change.Item));
                                System.Diagnostics.Debug.WriteLine($"[UnifiedBrowser] Added: {change.Item.FileName}");
                            }
                        }
                        else if (change.Type == ItemChangeData<AssetEntity>.ChangeType.Deleted)
                        {
                            var toRemove = _collection.FirstOrDefault(a => a.FilePath == change.Item.FilePath);
                            if (toRemove != null)
                            {
                                _collection.Remove(toRemove);
                                System.Diagnostics.Debug.WriteLine($"[UnifiedBrowser] Removed: {change.Item.FileName}");
                            }
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

            // Check Color Filter
            if (criteria.ColorR.HasValue && criteria.ColorG.HasValue && criteria.ColorB.HasValue && asset.Colors != null)
            {
                int r = criteria.ColorR.Value;
                int g = criteria.ColorG.Value;
                int b = criteria.ColorB.Value;
                int t = criteria.ColorTolerance;

                int minR = Math.Max(0, r - t);
                int maxR = Math.Min(255, r + t);
                int minG = Math.Max(0, g - t);
                int maxG = Math.Min(255, g + t);
                int minB = Math.Max(0, b - t);
                int maxB = Math.Min(255, b + t);

                bool match = false;
                foreach (var c in asset.Colors)
                {
                    if (c.R >= minR && c.R <= maxR &&
                        c.G >= minG && c.G <= maxG &&
                        c.B >= minB && c.B <= maxB)
                    {
                        match = true;
                        break;
                    }
                }
                if (!match) return false;
            }

            return true;
        }

        #endregion
    }

}

