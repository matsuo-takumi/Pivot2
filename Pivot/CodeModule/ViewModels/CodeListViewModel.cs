using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Engine.Models;
using Pivot.Models;
using Pivot.Services;
using Pivot.Messages;
using Pivot.Engine.Messages;
using Pivot.CodeModule.Helpers;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;

namespace Pivot.CodeModule.ViewModels
{
    public partial class CodeListViewModel : ObservableObject
    {
        private readonly CodeService _codeService;
        private readonly AssetQueryService _queryService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private ObservableCollection<AssetEntity> _snippets = new();

        [ObservableProperty]
        private AssetEntity? _selectedSnippet;

        // Current filter state
        private string _activeTagFilter = string.Empty;
        
        [ObservableProperty]
        private string _searchQuery = string.Empty;

        // Debounce timer for saving reorder
        private System.Threading.Timer? _saveDebounceTimer;
        private const int SaveDebounceMs = 500;

        [ObservableProperty]
        private ObservableCollection<SortOption> _sortOptions = new()
        {
            new SortOption { Label = "Manual", Value = "manual" },
            new SortOption { Label = "Date (Newest)", Value = "date_desc" },
            new SortOption { Label = "Date (Oldest)", Value = "date_asc" },
            new SortOption { Label = "Name (A-Z)", Value = "name_asc" },
            new SortOption { Label = "Name (Z-A)", Value = "name_desc" },
        };

        [ObservableProperty]
        private SortOption _selectedSortOption;

        public CodeListViewModel(CodeService codeService, AssetQueryService queryService, IMessenger messenger)
        {
            _codeService = codeService;
            _queryService = queryService;
            _messenger = messenger;
            
            _selectedSortOption = SortOptions.First(); // Default to Manual


            // Subscribe to bulk items changed messages
            _messenger.Register<BulkItemsChangedMessage<AssetEntity>>(this, OnAssetsChanged);
        }

        private void OnAssetsChanged(object recipient, BulkItemsChangedMessage<AssetEntity> message)
        {
            foreach (var change in message.Value)
            {
                if (change.Type == ItemChangeData<AssetEntity>.ChangeType.Deleted)
                {
                    // Remove from collections when deleted from editor
                    var item = Snippets.FirstOrDefault(s => s.Id == change.Item?.Id);
                    if (item != null)
                    {
                         Snippets.Remove(item);
                    }
                }
            }
        }





        /// <summary>
        /// Reordering allowed only when no filter/search active AND Manual sort is selected
        /// </summary>
        public bool CanReorder => string.IsNullOrEmpty(_activeTagFilter) && 
                                  string.IsNullOrEmpty(_searchQuery) &&
                                  SelectedSortOption?.Value == "manual";

        #region CRUD Operations



        [RelayCommand]
        private async Task DeleteItemAsync(AssetEntity item)
        {
            if (item == null) return;
            Snippets.Remove(item);
            await _codeService.DeleteSnippetAsync(item);
        }

        [RelayCommand]
        private async Task TogglePinAsync(AssetEntity item)
        {
            if (item == null) return;
            
            item.IsFavorite = !item.IsFavorite;
            await _codeService.SaveSnippetAsync(item, saveToDisk: false);
            
            // Reload to resort
            await LoadSnippetsAsync();
        }

        #endregion

        #region Loading & Filtering

        public async Task LoadSnippetsAsync()
        {
            try
            {
                var criteria = new FilterCriteria
                {
                    TargetKind = AssetKind.Code,
                    SearchQuery = _searchQuery,
                    SortField = GetSortField(),
                    SortDirection = GetSortDirection()
                };

                if (!string.IsNullOrEmpty(_activeTagFilter))
                {
                    criteria.Tags = new HashSet<string> { _activeTagFilter };
                    criteria.TagMode = TagMatchMode.Any;
                }

                // Load first 500 items for performance
                var assets = await _queryService.QueryAsync(criteria, 0, 500);
                
                Snippets.Clear();
                foreach (var asset in assets)
                {
                    Snippets.Add(asset);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadSnippetsAsync failed: {ex.Message}");
            }
        }

        private SortField GetSortField()
        {
            return SelectedSortOption?.Value switch
            {
                "date_desc" or "date_asc" => SortField.Date,
                "name_asc" or "name_desc" => SortField.Name,
                "manual" => SortField.SortOrder,
                _ => SortField.Date
            };
        }

        private SortDirection GetSortDirection()
        {
            return SelectedSortOption?.Value switch
            {
                "date_asc" or "name_asc" or "manual" => SortDirection.Ascending,
                _ => SortDirection.Descending
            };
        }

        partial void OnSelectedSortOptionChanged(SortOption value)
        {
            _ = LoadSnippetsAsync();
            OnPropertyChanged(nameof(CanReorder));
        }

        partial void OnSearchQueryChanged(string value)
        {
            _ = LoadSnippetsAsync();
            OnPropertyChanged(nameof(CanReorder));
        }

        public void FilterByTag(string? tag)
        {
            _activeTagFilter = tag ?? string.Empty;
            _ = LoadSnippetsAsync();
            OnPropertyChanged(nameof(CanReorder));
        }

        private void ApplyFilters()
        {
            // Deprecated: Logic moved to LoadSnippetsAsync
        }

        #endregion

        #region Reordering (GridView-driven)

        /// <summary>
        /// Called by GridView's internal reordering when user drags items.
        /// We detect changes and debounce-save.
        /// </summary>
        private void OnSnippetsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Only save reorder if allowed and it's a move/reset action
            if (!CanReorder) return;
            
            // Debounce the save to avoid spamming during rapid reorders
            _saveDebounceTimer?.Dispose();
            _saveDebounceTimer = new System.Threading.Timer(
                async _ => await SaveSortOrderAsync(),
                null,
                SaveDebounceMs,
                System.Threading.Timeout.Infinite);
        }

        /// <summary>
        /// Saves current UI order to database.
        /// </summary>
        private async Task SaveSortOrderAsync()
        {
            // Sync _allSnippets - Removed as we use DB source
            // _allSnippets = Snippets.ToList();

            var tasks = new List<Task>();
            for (int i = 0; i < Snippets.Count; i++)
            {
                var s = Snippets[i];
                if (s.SortOrder != i)
                {
                    s.SortOrder = i;
                    tasks.Add(_codeService.SaveSnippetAsync(s, saveToDisk: false));
                }
            }
            
            if (tasks.Any())
            {
                await Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// Context menu: Move Up
        /// </summary>
        [RelayCommand]
        private void MoveItemUp(AssetEntity item)
        {
            if (!CanReorder) return;
            var index = Snippets.IndexOf(item);
            if (index > 0)
            {
                Snippets.Move(index, index - 1);
            }
        }

        /// <summary>
        /// Context menu: Move Down
        /// </summary>
        [RelayCommand]
        private void MoveItemDown(AssetEntity item)
        {
            if (!CanReorder) return;
            var index = Snippets.IndexOf(item);
            if (index < Snippets.Count - 1)
            {
                Snippets.Move(index, index + 1);
            }
        }

        #endregion
    }



    public class SortOption
    {
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
    }
}
