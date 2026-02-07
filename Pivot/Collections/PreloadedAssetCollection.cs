using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Models;
using Pivot.Services;

namespace Pivot.Collections
{
    /// <summary>
    /// ObservableCollection that pre-loads all assets into memory for instant client-side filtering.
    /// Similar to Eagle's approach - trades initial load time for instant subsequent operations.
    /// </summary>
    public class PreloadedAssetCollection : ObservableCollection<AssetEntity>
    {
        private List<AssetEntity> _allAssets = new();
        private FilterCriteria? _currentCriteria;

        /// <summary>
        /// Event fired when loading state changes.
        /// </summary>
        public event EventHandler<bool>? LoadingStateChanged;

        /// <summary>
        /// Event fired when counts are updated.
        /// </summary>
        public event EventHandler<(int Loaded, int Total)>? CountsUpdated;

        /// <summary>
        /// Load ALL assets for the target kind into memory at startup.
        /// This is a one-time cost for instant subsequent filtering.
        /// </summary>
        public async Task LoadAllAsync(AssetKind kind, IServiceProvider services)
        {
            LoadingStateChanged?.Invoke(this, true);

            try
            {
                using var scope = services.CreateScope();
                var queryService = scope.ServiceProvider.GetRequiredService<AssetQueryService>();

                // Load ALL assets (no pagination, no filtering except Kind)
                var criteria = new FilterCriteria { TargetKind = kind };
                _allAssets = await queryService.QueryAsync(criteria, skip: 0, take: int.MaxValue);

                System.Diagnostics.Debug.WriteLine($"[PreloadedCollection] Loaded {_allAssets.Count} assets into memory");

                // Initial display: show all
                ApplyFilter(criteria);
            }
            finally
            {
                LoadingStateChanged?.Invoke(this, false);
            }
        }

        /// <summary>
        /// INSTANT client-side filtering using LINQ - no database query.
        /// This is the key to Eagle-like performance.
        /// </summary>
        public void ApplyFilter(FilterCriteria criteria)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _currentCriteria = criteria;

            // LINQ filter on in-memory collection
            var filtered = _allAssets.Where(asset => MatchesCriteria(asset, criteria));

            // Apply sorting
            var sorted = ApplySorting(filtered, criteria).ToList();

            sw.Stop();
            System.Diagnostics.Debug.WriteLine($"[PreloadedCollection] Filter+Sort took {sw.ElapsedMilliseconds}ms");

            // CRITICAL PERFORMANCE FIX: Use batch update instead of Clear+foreach
            // Clear+foreach triggers thousands of CollectionChanged events
            // Batch update triggers only ONE event
            sw.Restart();
            UpdateCollectionBatch(sorted);
            sw.Stop();
            System.Diagnostics.Debug.WriteLine($"[PreloadedCollection] Collection update took {sw.ElapsedMilliseconds}ms");

            // Update counts
            CountsUpdated?.Invoke(this, (Count, _allAssets.Count));

            System.Diagnostics.Debug.WriteLine($"[PreloadedCollection] Filtered to {Count} items (from {_allAssets.Count} total)");
        }

        /// <summary>
        /// Efficiently update the collection with a new set of items.
        /// Uses a single CollectionChanged event instead of thousands.
        /// </summary>
        private void UpdateCollectionBatch(List<AssetEntity> newItems)
        {
            // Suppress change notifications during bulk update
            CheckReentrancy();

            // Clear and rebuild the internal list
            Items.Clear();
            foreach (var item in newItems)
            {
                Items.Add(item);
            }

            // Fire a single Reset event to notify UI
            OnCollectionChanged(new System.Collections.Specialized.NotifyCollectionChangedEventArgs(
                System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Check if an asset matches the current filter criteria.
        /// </summary>
        private bool MatchesCriteria(AssetEntity asset, FilterCriteria criteria)
        {
            // Directory filter
            if (!string.IsNullOrEmpty(criteria.Directory))
            {
                var assetDir = Path.GetDirectoryName(asset.FilePath)?.Replace('/', '\\').TrimEnd('\\');
                var filterDir = criteria.Directory.Replace('/', '\\').TrimEnd('\\');

                // Check exact match or subdirectory
                if (!string.Equals(assetDir, filterDir, StringComparison.OrdinalIgnoreCase) &&
                    !(assetDir?.StartsWith(filterDir + "\\", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    return false;
                }
            }

            // Search query filter
            if (!string.IsNullOrEmpty(criteria.SearchQuery))
            {
                var searchLower = criteria.SearchQuery.ToLowerInvariant();
                if (!(asset.FileName?.ToLowerInvariant().Contains(searchLower) ?? false))
                {
                    return false;
                }
            }

            // Rating filter
            if (criteria.MinRating > 0 && asset.Rating < criteria.MinRating)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Apply sorting to filtered results.
        /// </summary>
        private IEnumerable<AssetEntity> ApplySorting(IEnumerable<AssetEntity> items, FilterCriteria criteria)
        {
            return criteria.SortField switch
            {
                SortField.Name => criteria.SortDirection == SortDirection.Ascending
                    ? items.OrderBy(a => a.FileName)
                    : items.OrderByDescending(a => a.FileName),
                SortField.Date => criteria.SortDirection == SortDirection.Ascending
                    ? items.OrderBy(a => a.LastModifiedUtc)
                    : items.OrderByDescending(a => a.LastModifiedUtc),
                SortField.Size => criteria.SortDirection == SortDirection.Ascending
                    ? items.OrderBy(a => a.FileSize)
                    : items.OrderByDescending(a => a.FileSize),
                SortField.Rating => criteria.SortDirection == SortDirection.Ascending
                    ? items.OrderBy(a => a.Rating)
                    : items.OrderByDescending(a => a.Rating),
                _ => items.OrderBy(a => a.SortOrder).ThenByDescending(a => a.LastModifiedUtc) // Default: custom order, then newest
            };
        }

        /// <summary>
        /// Add new asset from file watcher.
        /// Automatically appears in filtered view if it matches current criteria.
        /// </summary>
        public void AddAsset(AssetEntity asset)
        {
            _allAssets.Add(asset);

            // If matches current filter, add to visible collection
            if (_currentCriteria != null && MatchesCriteria(asset, _currentCriteria))
            {
                // Insert at beginning for immediate visibility (newest first)
                Insert(0, asset);
                CountsUpdated?.Invoke(this, (Count, _allAssets.Count));
            }
        }

        /// <summary>
        /// Remove deleted asset.
        /// </summary>
        public void RemoveAsset(string filePath)
        {
            var asset = _allAssets.FirstOrDefault(a => a.FilePath == filePath);
            if (asset != null)
            {
                _allAssets.Remove(asset);
                Remove(asset);
                CountsUpdated?.Invoke(this, (Count, _allAssets.Count));
            }
        }

        /// <summary>
        /// Update existing asset (e.g., metadata changed).
        /// </summary>
        public void UpdateAsset(AssetEntity updatedAsset)
        {
            var existing = _allAssets.FirstOrDefault(a => a.FilePath == updatedAsset.FilePath);
            if (existing != null)
            {
                var index = _allAssets.IndexOf(existing);
                _allAssets[index] = updatedAsset;

                // Re-apply filter to update visible collection
                if (_currentCriteria != null)
                {
                    ApplyFilter(_currentCriteria);
                }
            }
        }
    }
}
