using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pivot.Models;
using Pivot.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Pivot.ViewModels
{
    /// <summary>
    /// タグ/フィルター設定を管理する共通ViewModel
    /// </summary>
    public partial class FilterSettingsViewModel : ObservableObject
    {
        private readonly FilterSettingsService _filterSettings;
        private readonly FilterType _filterType;
        private readonly string _tabId;
        private readonly ILogger<FilterSettingsViewModel>? _logger;
        private readonly Func<List<CustomFilter>>? _getDefaultFilters;

        public ObservableCollection<FilterItemViewModel> Filters { get; } = new();

        [ObservableProperty]
        private bool _isLoading;

        public bool ShowExtensions => _filterType == FilterType.Asset || _filterType == FilterType.Image;

        public string FilterTypeDisplayName => _filterType switch
        {
            FilterType.Asset => "Asset Filters",
            FilterType.Code => "Code Tags (Filters)",
            FilterType.Image => "Image Tags (Filters)",
            _ => "Filters"
        };

        public FilterSettingsViewModel(
            FilterSettingsService filterSettings,
            FilterType filterType,
            string tabId,
            Func<List<CustomFilter>>? getDefaultFilters = null,
            ILogger<FilterSettingsViewModel>? logger = null)
        {
            _filterSettings = filterSettings ?? throw new ArgumentNullException(nameof(filterSettings));
            _filterType = filterType;
            _tabId = tabId ?? throw new ArgumentNullException(nameof(tabId));
            _getDefaultFilters = getDefaultFilters;
            _logger = logger;

            LoadFilters();
        }

        public void LoadFilters()
        {
            try
            {
                IsLoading = true;
                Filters.Clear();

                var allFilters = GetFiltersForType(_filterType);
                var visibleFilterIds = _filterSettings.GetVisibleFiltersForTab(_tabId) ?? new List<Guid>();

                foreach (var filter in allFilters)
                {
                    Filters.Add(new FilterItemViewModel
                    {
                        Id = filter.Id,
                        Name = filter.Name,
                        AllowedExtensions = filter.AllowedExtensions ?? new List<string>(),
                        IsBuiltIn = filter.IsBuiltIn,
                        Visible = visibleFilterIds.Contains(filter.Id)
                    });
                }

                _logger?.LogInformation("Loaded {Count} filters for {FilterType} (Tab: {TabId})", Filters.Count, _filterType, _tabId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load filters for {FilterType}", _filterType);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ToggleVisibilityAsync(FilterItemViewModel? item)
        {
            if (item == null) return;

            try
            {
                var current = _filterSettings.GetVisibleFiltersForTab(_tabId) ?? new List<Guid>();
                if (item.Visible)
                {
                    if (!current.Contains(item.Id)) current.Add(item.Id);
                }
                else
                {
                    current.Remove(item.Id);
                }
                await _filterSettings.SetVisibleFiltersForTabAsync(_tabId, current);
                LoadFilters();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to toggle visibility for filter {Id}", item.Id);
            }
        }

        [RelayCommand]
        private Task AddFilterAsync()
        {
            // UIからダイアログを表示するため、このメソッドは空実装
            // 実際の追加処理はView側のイベントハンドラーで行う
            return Task.CompletedTask;
        }

        public async Task AddFilterAsync(string name, List<string>? extensions = null)
        {
            try
            {
                var allFilters = GetFiltersForType(_filterType);
                
                // 名前の重複チェック
                if (!string.IsNullOrWhiteSpace(name) && allFilters.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger?.LogWarning("Filter with name '{Name}' already exists", name);
                    return;
                }

                var newFilter = new CustomFilter
                {
                    Name = name ?? "New Filter",
                    AllowedExtensions = extensions ?? new List<string>()
                };

                allFilters.Add(newFilter);
                await SaveFiltersForTypeAsync(_filterType, allFilters);
                LoadFilters();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to add filter");
            }
        }

        [RelayCommand]
        private async Task EditFilterAsync((Guid FilterId, string Name, List<string> Extensions) args)
        {
            try
            {
                var allFilters = GetFiltersForType(_filterType);
                var target = allFilters.FirstOrDefault(f => f.Id == args.FilterId);
                if (target == null) return;

                target.Name = args.Name;
                target.AllowedExtensions = args.Extensions ?? new List<string>();
                await SaveFiltersForTypeAsync(_filterType, allFilters);
                LoadFilters();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to edit filter {Id}", args.FilterId);
            }
        }

        [RelayCommand]
        private async Task DeleteFilterAsync(Guid filterId)
        {
            try
            {
                var allFilters = GetFiltersForType(_filterType);
                var target = allFilters.FirstOrDefault(f => f.Id == filterId);
                if (target == null) return;
                if (target.IsBuiltIn) return; // ビルトインフィルターは削除不可

                var tagName = target.Name;
                allFilters.Remove(target);
                await SaveFiltersForTypeAsync(_filterType, allFilters);

                // Codeフィルター削除時は、CodeRepositoryからもタグを削除
                if (_filterType == FilterType.Code && !string.IsNullOrWhiteSpace(tagName))
                {
                    try
                    {
                        // Use CodeService to remove tag from all snippets
                        var codeService = App.Current.Services.GetService(typeof(Pivot.Services.CodeService)) as Pivot.Services.CodeService;
                        if (codeService != null)
                        {
                            var all = await codeService.GetAllSnippetsAsync();
                            foreach (var snippet in all.Where(s => s.Tags?.Contains(tagName, StringComparison.OrdinalIgnoreCase) == true))
                            {
                                var tags = snippet.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(t => t.Trim())
                                    .Where(t => !string.Equals(t, tagName, StringComparison.OrdinalIgnoreCase))
                                    .Distinct(StringComparer.OrdinalIgnoreCase);
                                snippet.Tags = string.Join(", ", tags);
                                await codeService.SaveSnippetAsync(snippet);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to remove tag from snippets: {TagName}", tagName);
                    }
                }

                LoadFilters();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to delete filter {Id}", filterId);
            }
        }

        [RelayCommand]
        private async Task RestoreDefaultsAsync()
        {
            try
            {
                if (_getDefaultFilters != null)
                {
                    var defaults = _getDefaultFilters();
                    await SaveFiltersForTypeAsync(_filterType, defaults);
                }
                else
                {
                    // デフォルトがない場合は空のリストにリセット
                    await SaveFiltersForTypeAsync(_filterType, new List<CustomFilter>());
                }
                LoadFilters();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to restore defaults");
            }
        }

        private List<CustomFilter> GetFiltersForType(FilterType type)
        {
            return type switch
            {
                FilterType.Asset => _filterSettings.GetAssetFilters(),
                FilterType.Code => _filterSettings.GetCodeFilters(),
                FilterType.Image => GetImageFilters(_filterSettings.GetAssetFilters()),
                _ => new List<CustomFilter>()
            };
        }

        private static List<CustomFilter> GetImageFilters(List<CustomFilter> assetFilters)
        {
            var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tga", ".tif", ".tiff"
            };

            var imageFilters = assetFilters
                .Where(f => f.AllowedExtensions.Any(ext => imageExtensions.Contains(ext)))
                .ToList();

            // 画像フィルターが見つからない場合は、すべてのフィルターを表示（後方互換性）
            return imageFilters.Count > 0 ? imageFilters : assetFilters;
        }

        private async Task SaveFiltersForTypeAsync(FilterType type, List<CustomFilter> filters)
        {
            switch (type)
            {
                case FilterType.Asset:
                    await _filterSettings.SetAssetFiltersAsync(filters);
                    break;
                case FilterType.Code:
                    await _filterSettings.SetCodeFiltersAsync(filters);
                    break;
                case FilterType.Image:
                    await SaveImageFiltersAsync(filters);
                    break;
            }
        }

        private async Task SaveImageFiltersAsync(List<CustomFilter> imageFilters)
        {
            var assetFilters = _filterSettings.GetAssetFilters();
            var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tga", ".tif", ".tiff"
            };

            var nonImageFilters = assetFilters
                .Where(f => !f.AllowedExtensions.Any(ext => imageExtensions.Contains(ext)))
                .ToList();

            var allFilters = nonImageFilters.Concat(imageFilters).ToList();
            await _filterSettings.SetAssetFiltersAsync(allFilters);
        }

        /// <summary>
        /// フィルター項目のViewModel
        /// </summary>
        public class FilterItemViewModel : ObservableObject
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public List<string> AllowedExtensions { get; set; } = new();
            public string AllowedExtensionsString => string.Join(", ", AllowedExtensions);
            public bool IsBuiltIn { get; set; }
            
            private bool _visible;
            public bool Visible
            {
                get => _visible;
                set => SetProperty(ref _visible, value);
            }
        }
    }
}

