using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Data;
using Pivot.Engine.Models;
using Pivot.Models;
using Pivot.Services;

namespace Pivot.Collections
{
    /// <summary>
    /// ObservableCollection that supports incremental loading for WinUI 3 virtualization.
    /// Loads assets in batches using AssetQueryService.
    /// </summary>
    public class IncrementalAssetCollection : ObservableCollection<AssetEntity>, ISupportIncrementalLoading
    {
        private readonly IServiceProvider _serviceProvider;
        private FilterCriteria _criteria;
        private int _totalCount;
        private bool _isLoading;
        private bool _hasMoreItems = true;

        public bool HasMoreItems => _hasMoreItems && !_isLoading;

        /// <summary>
        /// Page size for incremental loading.
        /// ItemsRepeater virtualizes rendering, so large page sizes are safe.
        /// </summary>
        public int PageSize { get; set; } = 10000;

        /// <summary>
        /// Event fired when loading state changes.
        /// </summary>
        public event EventHandler<bool>? LoadingStateChanged;

        /// <summary>
        /// Event fired when counts are updated.
        /// </summary>
        public event EventHandler<(int Loaded, int Total)>? CountsUpdated;

        public IncrementalAssetCollection(IServiceProvider serviceProvider, FilterCriteria criteria)
        {
            _serviceProvider = serviceProvider;
            _criteria = criteria.Clone();
            
            // Load PageSize from settings
            var themeSettings = serviceProvider.GetService<ThemeSettingsService>();
            if (themeSettings != null)
            {
                PageSize = themeSettings.PageSize;
            }
        }

        /// <summary>
        /// Reset the collection with new criteria.
        /// Loads initial batch before updating UI to prevent flickering.
        /// </summary>
        public async Task ResetAsync(FilterCriteria criteria)
        {
            _criteria = criteria.Clone();
            _hasMoreItems = true;
            _isLoading = true;
            LoadingStateChanged?.Invoke(this, true);

            try
            {
                System.Diagnostics.Debug.WriteLine($"[IncrementalCollection] ResetAsync - Directory: '{_criteria.Directory ?? "(null)"}', Kind: {_criteria.TargetKind}");

                // Get total count and initial batch BEFORE clearing
                using var scope = _serviceProvider.CreateScope();
                var queryService = scope.ServiceProvider.GetRequiredService<AssetQueryService>();
                _totalCount = await queryService.CountAsync(_criteria);

                System.Diagnostics.Debug.WriteLine($"[IncrementalCollection] Total count: {_totalCount}");

                // Load initial batch
                var initialItems = _totalCount > 0 
                    ? await queryService.QueryAsync(_criteria, 0, PageSize)
                    : new System.Collections.Generic.List<AssetEntity>();

                // Now clear and populate in one batch to prevent flickering
                Clear();
                foreach (var item in initialItems)
                {
                    Add(item);
                }

                _hasMoreItems = Count < _totalCount;
                CountsUpdated?.Invoke(this, (Count, _totalCount));
            }
            finally
            {
                _isLoading = false;
                LoadingStateChanged?.Invoke(this, false);
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return LoadMoreItemsInternalAsync(count).AsAsyncOperation();
        }

        private async Task<LoadMoreItemsResult> LoadMoreItemsInternalAsync(uint count)
        {
            if (_isLoading || !_hasMoreItems)
            {
                return new LoadMoreItemsResult { Count = 0 };
            }

            _isLoading = true;
            LoadingStateChanged?.Invoke(this, true);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var queryService = scope.ServiceProvider.GetRequiredService<AssetQueryService>();

                var skip = Count;
                var take = (int)Math.Min(count, PageSize);

                var items = await queryService.QueryAsync(_criteria, skip, take);

                foreach (var item in items)
                {
                    Add(item);
                }

                _hasMoreItems = Count < _totalCount;
                CountsUpdated?.Invoke(this, (Count, _totalCount));

                return new LoadMoreItemsResult { Count = (uint)items.Count };
            }
            finally
            {
                _isLoading = false;
                LoadingStateChanged?.Invoke(this, false);
            }
        }
    }
}
