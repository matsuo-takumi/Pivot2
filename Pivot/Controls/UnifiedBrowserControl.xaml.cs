using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
        private IncrementalAssetCollection? _collection;
        private LayoutBasedTemplateSelector? _templateSelector;
        private CancellationTokenSource? _searchDebounceToken;
        private bool _sortAscending = false;

        /// <summary>
        /// The target asset kind to display (Image, Code, etc).
        /// </summary>
        public AssetKind? TargetKind { get; set; }

        // Interaction Events
        public event EventHandler<AssetEntity>? ItemClicked;
        public event EventHandler<AssetEntity>? ItemDoubleClicked;
        public event EventHandler<(AssetEntity Asset, Windows.Foundation.Point Position)>? ItemRightTapped;
        public event DragItemsStartingEventHandler? ItemDragStarting;

        public UnifiedBrowserControl()
        {
            this.InitializeComponent();
            _templateSelector = Resources["LayoutTemplateSelector"] as LayoutBasedTemplateSelector;
            
            // Register for real-time asset updates
            WeakReferenceMessenger.Default.Register<BulkItemsChangedMessage<AssetEntity>>(this);
            WeakReferenceMessenger.Default.Register<DirectoryRemovedMessage>(this);
            this.Unloaded += (s, e) => WeakReferenceMessenger.Default.UnregisterAll(this);
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

        #region Criteria Change Handlers

        private async void OnCriteriaChanged(object? sender, EventArgs e)
        {
            if (_viewModel == null || _collection == null) return;

            System.Diagnostics.Debug.WriteLine($"[UnifiedBrowser] OnCriteriaChanged - Directory: '{_viewModel.FilterCriteria.Directory ?? "(null)"}'");
            await _collection.ResetAsync(_viewModel.FilterCriteria);
            UpdateEmptyState();
        }

        private void OnLoadingStateChanged(object? sender, bool isLoading)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                LoadingRing.IsActive = isLoading;
                if (_viewModel != null)
                {
                    _viewModel.IsLoading = isLoading;
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
        private AssetEntity? FindAssetFromElement(DependencyObject? element)
        {
            while (element != null)
            {
                if (element is FrameworkElement fe && fe.DataContext is AssetEntity asset)
                {
                    return asset;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        // Helper to find the Border element for selection visual
        private Border? FindItemBorder(DependencyObject? element)
        {
            while (element != null)
            {
                if (element is Border border && border.Name == "ItemBorder")
                {
                    return border;
                }
                if (element is FrameworkElement fe && fe.DataContext is AssetEntity)
                {
                    // We're at the item level, search children for ItemBorder
                    return FindChildBorder(fe);
                }
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        private Border? FindChildBorder(DependencyObject parent)
        {
            if (parent is Border b && b.Name == "ItemBorder") return b;
            
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var result = FindChildBorder(child);
                if (result != null) return result;
            }
            return null;
        }

        private void AssetRepeater_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var asset = FindAssetFromElement(e.OriginalSource as DependencyObject);
            if (asset != null)
            {
                System.Diagnostics.Debug.WriteLine($"[Tapped] {asset.FileName}");
                
                // Update selection visual
                ClearSelectionVisuals();
                var border = FindItemBorder(e.OriginalSource as DependencyObject);
                if (border != null)
                {
                    border.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
                    _selectedBorder = border;
                }
                
                _selectedAsset = asset;
                ItemClicked?.Invoke(this, asset);
            }
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

        private AssetEntity? _selectedAsset;
        private Border? _selectedBorder;

        private void ClearSelectionVisuals()
        {
            if (_selectedBorder != null)
            {
                _selectedBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                _selectedBorder = null;
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
                LayoutType.Masonry => new StaggeredLayout
                {
                    DesiredColumnWidth = 220,
                    ColumnSpacing = 8,
                    RowSpacing = 8
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
                            // Check if already exists
                            var existing = _collection.FirstOrDefault(a => a.FilePath == change.Item.FilePath);
                            if (existing == null)
                            {
                                // Add new item at the beginning for immediate visibility
                                _collection.Insert(0, change.Item);
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

            return true;
        }

        #endregion
    }

}

