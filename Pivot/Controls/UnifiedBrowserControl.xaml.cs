using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using CommunityToolkit.WinUI.Controls;
using Pivot.Collections;
using Pivot.Models;
using Pivot.Services;
using Pivot.ViewModels;

namespace Pivot.Controls
{
    /// <summary>
    /// Unified browser control for displaying assets with virtualized scrolling.
    /// Supports Masonry, Grid, and List layouts with filtering.
    /// </summary>
    public sealed partial class UnifiedBrowserControl : UserControl
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

        #region Item Interaction Handlers

        private void Item_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is AssetEntity asset)
            {
                ItemClicked?.Invoke(this, asset);
            }
        }

        private void Item_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is AssetEntity asset)
            {
                ItemDoubleClicked?.Invoke(this, asset);
            }
        }

        private void Item_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is AssetEntity asset)
            {
                var position = e.GetPosition(this);
                ItemRightTapped?.Invoke(this, (asset, position));
            }
        }

        private void Item_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            if (sender is FrameworkElement element && element.DataContext is AssetEntity asset)
            {
                args.Data.SetText(asset.FilePath ?? asset.FileName);
                args.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
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

            // Force template refresh by resetting ItemsSource
            var source = AssetRepeater.ItemsSource;
            AssetRepeater.ItemsSource = null;
            AssetRepeater.ItemsSource = source;
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
    }

    /// <summary>
    /// Helper class for parsing color strings.
    /// </summary>
    public static class ColorHelper
    {
        public static Windows.UI.Color ParseColor(string? hexColor)
        {
            if (string.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#"))
            {
                return Windows.UI.Color.FromArgb(255, 64, 64, 64); // Default dark gray
            }

            try
            {
                hexColor = hexColor.TrimStart('#');
                if (hexColor.Length == 6)
                {
                    byte r = Convert.ToByte(hexColor.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hexColor.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hexColor.Substring(4, 2), 16);
                    return Windows.UI.Color.FromArgb(255, r, g, b);
                }
            }
            catch { }

            return Windows.UI.Color.FromArgb(255, 64, 64, 64);
        }
    }

    /// <summary>
    /// Helper class for formatting file sizes.
    /// </summary>
    public static class FileSizeHelper
    {
        public static string FormatSize(long? bytes)
        {
            if (!bytes.HasValue || bytes.Value <= 0) return "-";

            string[] sizes = { "B", "KB", "MB", "GB" };
            double value = bytes.Value;
            int order = 0;

            while (value >= 1024 && order < sizes.Length - 1)
            {
                order++;
                value /= 1024;
            }

            return $"{value:0.#} {sizes[order]}";
        }
    }
}
