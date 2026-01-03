using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Pivot.Messages;
using Pivot.Models;

namespace Pivot.Services
{
    /// <summary>
    /// フィルター設定の管理サービス。
    /// AssetFilters, CodeFilters, VisibleFilters, SelectedFilters を管理。
    /// </summary>
    public class FilterSettingsService
    {
        private readonly ILogger<FilterSettingsService> _logger;
        private readonly ISettingsStore _settingsStore;
        private readonly IMessenger? _messenger;

        // In-memory cache
        private List<CustomFilter> _assetFilters = new();
        private List<CustomFilter> _codeFilters = new();

        private Dictionary<string, List<Guid>> _visibleFiltersByTab = new();
        private Dictionary<string, List<Guid>> _selectedFiltersByTab = new();

        public FilterSettingsService(
            ILogger<FilterSettingsService> logger,
            ISettingsStore settingsStore,
            IMessenger? messenger = null)
        {
            _logger = logger;
            _settingsStore = settingsStore;
            _messenger = messenger;
        }

        public async Task LoadAsync()
        {
            _logger.LogInformation("FilterSettingsService: Loading...");

            try
            {
                // AssetFilters
                var assetFiltersJson = await _settingsStore.GetAsync("AssetFilters");
                if (!string.IsNullOrEmpty(assetFiltersJson))
                {
                    _assetFilters = JsonSerializer.Deserialize<List<CustomFilter>>(assetFiltersJson) ?? new();
                }
                else
                {
                    _assetFilters = GetDefaultAssetFilters();
                }

                // CodeFilters
                var codeFiltersJson = await _settingsStore.GetAsync("CodeFilters");
                if (!string.IsNullOrEmpty(codeFiltersJson))
                {
                    _codeFilters = JsonSerializer.Deserialize<List<CustomFilter>>(codeFiltersJson) ?? new();
                }
                else
                {
                    _codeFilters = GetDefaultCodeFilters();
                }



                // VisibleFiltersByTab
                var visibleJson = await _settingsStore.GetAsync("VisibleFiltersByTab");
                if (!string.IsNullOrEmpty(visibleJson))
                {
                    _visibleFiltersByTab = JsonSerializer.Deserialize<Dictionary<string, List<Guid>>>(visibleJson) ?? new();
                }

                // SelectedFiltersByTab
                var selectedJson = await _settingsStore.GetAsync("SelectedFiltersByTab");
                if (!string.IsNullOrEmpty(selectedJson))
                {
                    _selectedFiltersByTab = JsonSerializer.Deserialize<Dictionary<string, List<Guid>>>(selectedJson) ?? new();
                }

                _logger.LogInformation("FilterSettingsService: Loaded {AssetFilter} asset filters, {CodeFilter} code filters",
                    _assetFilters.Count, _codeFilters.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FilterSettingsService: Failed to load, using defaults.");
                _assetFilters = GetDefaultAssetFilters();
                _codeFilters = GetDefaultCodeFilters();
                _codeFilters = GetDefaultCodeFilters();
            }
        }

        // =============== Asset Filters ===============

        public List<CustomFilter> GetAssetFilters() => _assetFilters;

        public async Task SetAssetFiltersAsync(List<CustomFilter> filters)
        {
            _assetFilters = filters ?? new List<CustomFilter>();
            try
            {
                await _settingsStore.UpsertAsync("AssetFilters", JsonSerializer.Serialize(_assetFilters));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FilterSettingsService: Failed to persist AssetFilters.");
            }
        }

        // =============== Code Filters ===============

        public List<CustomFilter> GetCodeFilters() => _codeFilters;

        public async Task SetCodeFiltersAsync(List<CustomFilter> filters)
        {
            _codeFilters = filters ?? new List<CustomFilter>();
            try
            {
                await _settingsStore.UpsertAsync("CodeFilters", JsonSerializer.Serialize(_codeFilters));
                _messenger?.Send(new CodeFiltersUpdatedMessage(_codeFilters));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FilterSettingsService: Failed to persist CodeFilters.");
            }
        }



        // =============== Filter Lookup ===============

        public string GetFilterNameById(Guid filterId)
        {
            var assetFilter = _assetFilters.FirstOrDefault(f => f.Id == filterId);
            if (assetFilter != null) return assetFilter.Name;

            var codeFilter = _codeFilters.FirstOrDefault(f => f.Id == filterId);
            if (codeFilter != null) return codeFilter.Name;

            return string.Empty;
        }

        public async Task<string?> UpdateFilterNameAsync(Guid filterId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return null;

            try
            {
                var filter = _codeFilters.FirstOrDefault(f => f.Id == filterId);
                if (filter == null) return null;

                var oldName = filter.Name;
                filter.Name = newName.Trim();
                await SetCodeFiltersAsync(_codeFilters);
                return oldName;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FilterSettingsService: Failed to update filter name.");
                return null;
            }
        }

        public async Task<string?> DeleteFilterAsync(Guid filterId)
        {
            try
            {
                var filter = _codeFilters.FirstOrDefault(f => f.Id == filterId);
                if (filter == null) return null;

                var oldName = filter.Name;
                _codeFilters.Remove(filter);
                await SetCodeFiltersAsync(_codeFilters);
                return oldName;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FilterSettingsService: Failed to delete filter.");
                return null;
            }
        }

        // =============== Visible Filters Per Tab ===============

        public List<Guid> GetVisibleFiltersForTab(string tabId)
        {
            if (string.IsNullOrWhiteSpace(tabId)) return new List<Guid>();
            if (_visibleFiltersByTab.TryGetValue(tabId, out var list)) return list;
            return new List<Guid>();
        }

        public async Task SetVisibleFiltersForTabAsync(string tabId, List<Guid> filterIds)
        {
            if (string.IsNullOrWhiteSpace(tabId)) return;
            _visibleFiltersByTab[tabId] = filterIds ?? new List<Guid>();
            try
            {
                await _settingsStore.UpsertAsync("VisibleFiltersByTab", JsonSerializer.Serialize(_visibleFiltersByTab));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FilterSettingsService: Failed to persist VisibleFiltersByTab.");
            }
        }

        // =============== Selected Filters Per Tab ===============

        public List<Guid> GetSelectedFiltersForTab(string tabId)
        {
            if (string.IsNullOrWhiteSpace(tabId)) return new List<Guid>();
            if (_selectedFiltersByTab.TryGetValue(tabId, out var list)) return list;
            return new List<Guid>();
        }

        public async Task SetSelectedFiltersForTabAsync(string tabId, List<Guid> filterIds)
        {
            if (string.IsNullOrWhiteSpace(tabId)) return;
            _selectedFiltersByTab[tabId] = filterIds ?? new List<Guid>();
            try
            {
                await _settingsStore.UpsertAsync("SelectedFiltersByTab", JsonSerializer.Serialize(_selectedFiltersByTab));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FilterSettingsService: Failed to persist SelectedFiltersByTab.");
            }
        }

        // =============== Defaults ===============

        private static List<CustomFilter> GetDefaultAssetFilters()
        {
            return new List<CustomFilter>();
        }

        private static List<CustomFilter> GetDefaultCodeFilters()
        {
            return new List<CustomFilter>();
        }


    }
}
