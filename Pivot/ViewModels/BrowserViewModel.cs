using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pivot.Collections;
using Pivot.Engine.Models;
using Pivot.Models;
using Pivot.Services;

namespace Pivot.ViewModels
{
    /// <summary>
    /// ViewModel for the UnifiedBrowserControl.
    /// Manages filter criteria, layout mode, and data loading.
    /// </summary>
    public partial class BrowserViewModel : ObservableObject
    {
        private readonly ILogger<BrowserViewModel> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly BrowserSettingsService _browserSettings;

        [ObservableProperty]
        private FilterCriteria _filterCriteria = new();

        [ObservableProperty]
        private LayoutType _currentLayout = LayoutType.Masonry;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private int _loadedCount;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusText = "Ready";

        /// <summary>
        /// Event fired when criteria changes and data should be reloaded.
        /// </summary>
        public event EventHandler? CriteriaChanged;

        public BrowserViewModel(
            ILogger<BrowserViewModel> logger,
            IServiceProvider serviceProvider,
            BrowserSettingsService browserSettings)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _browserSettings = browserSettings;

            // Load default settings
            _currentLayout = _browserSettings.DefaultLayoutMode;
            _filterCriteria.SortField = _browserSettings.DefaultSortField;
            _filterCriteria.SortDirection = _browserSettings.SortDirection;
        }

        /// <summary>
        /// Set the target asset kind (Image, Code, etc).
        /// </summary>
        public void SetTargetKind(AssetKind? kind)
        {
            if (FilterCriteria.TargetKind != kind)
            {
                FilterCriteria.TargetKind = kind;
                OnCriteriaChanged();
            }
        }

        /// <summary>
        /// Set search query for file name filtering.
        /// </summary>
        public void SetSearchQuery(string? query)
        {
            if (FilterCriteria.SearchQuery != query)
            {
                FilterCriteria.SearchQuery = query;
                OnCriteriaChanged();
            }
        }

        /// <summary>
        /// Set minimum rating filter.
        /// </summary>
        public void SetMinRating(int rating)
        {
            if (FilterCriteria.MinRating != rating)
            {
                FilterCriteria.MinRating = rating;
                OnCriteriaChanged();
            }
        }

        /// <summary>
        /// Change sort field and direction.
        /// </summary>
        public void SetSorting(SortField field, SortDirection direction)
        {
            bool changed = FilterCriteria.SortField != field || FilterCriteria.SortDirection != direction;
            if (changed)
            {
                FilterCriteria.SortField = field;
                FilterCriteria.SortDirection = direction;
                OnCriteriaChanged();
            }
        }

        /// <summary>
        /// Set directory filter to show only assets from a specific folder.
        /// Pass null to clear the filter (show all).
        /// </summary>
        public void SetDirectoryFilter(string? directoryPath)
        {
            bool wasFiltered = FilterCriteria.Directory != null;
            bool isClearing = directoryPath == null;
            
            if (FilterCriteria.Directory != directoryPath || (isClearing && wasFiltered))
            {
                FilterCriteria.Directory = directoryPath;
                _logger.LogDebug("BrowserViewModel: Directory filter set to {Directory}", directoryPath ?? "(all)");
                OnCriteriaChanged();
            }
            else if (isClearing)
            {
                // Force refresh even if already null (user clicked "Show All")
                OnCriteriaChanged();
            }
        }

        /// <summary>
        /// Replace current criteria with new criteria (e.g. from Smart Folder).
        /// Preserves transient state if needed, or fully replaces.
        /// </summary>
        public void SetCriteria(FilterCriteria newCriteria)
        {
            // Clone to ensure we don't modify the source (e.g. Smart Folder definition) inadvertently
            FilterCriteria = newCriteria.Clone();
            _logger.LogDebug("BrowserViewModel: Criteria replaced from Smart Folder/External source");
            OnCriteriaChanged();
        }

        public void SetColorFilter(byte r, byte g, byte b, int tolerance = 30)
        {
            FilterCriteria.ColorR = r;
            FilterCriteria.ColorG = g;
            FilterCriteria.ColorB = b;
            FilterCriteria.ColorTolerance = tolerance;
            _logger.LogDebug("BrowserViewModel: Color filter set to R={R}, G={G}, B={B}, Tol={T}", r, g, b, tolerance);
            OnCriteriaChanged();
        }

        public void ClearColorFilter()
        {
            if (FilterCriteria.ColorR == null) return;
            
            FilterCriteria.ColorR = null;
            FilterCriteria.ColorG = null;
            FilterCriteria.ColorB = null;
            _logger.LogDebug("BrowserViewModel: Color filter cleared");
            OnCriteriaChanged();
        }

        partial void OnCurrentLayoutChanged(LayoutType value)
        {
            _logger.LogDebug("BrowserViewModel: Layout changed to {Layout}", value);
            Utilities.SafeAsync.FireAndForget(
                _browserSettings.SetDefaultLayoutModeAsync(value),
                nameof(OnCurrentLayoutChanged));
            UpdateStatusText();
        }

        [RelayCommand]
        private void CycleLayout()
        {
            CurrentLayout = CurrentLayout switch
            {
                LayoutType.Masonry => LayoutType.Grid,
                LayoutType.Grid => LayoutType.List,
                LayoutType.List => LayoutType.Masonry,
                _ => LayoutType.Masonry
            };
        }

        [RelayCommand]
        private void Refresh()
        {
            OnCriteriaChanged();
        }

        private void OnCriteriaChanged()
        {
            _logger.LogDebug("BrowserViewModel: Criteria changed, firing event");
            CriteriaChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Update counts after loading.
        /// </summary>
        public void UpdateCounts(int loaded, int total)
        {
            LoadedCount = loaded;
            TotalCount = total;
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            if (IsLoading)
            {
                StatusText = "Loading...";
            }
            else if (TotalCount == 0)
            {
                StatusText = "No items";
            }
            else
            {
                StatusText = LoadedCount < TotalCount 
                    ? $"{LoadedCount} / {TotalCount} items"
                    : $"{TotalCount} items";
            }
        }
    }
}
