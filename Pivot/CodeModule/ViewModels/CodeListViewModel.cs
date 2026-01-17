using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Models;
using Pivot.Services;
using Pivot.Messages;
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
        private readonly IMessenger _messenger;

        // Full dataset (source of truth for filtering)
        private List<AssetEntity> _allSnippets = new();

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

        public CodeListViewModel(CodeService codeService, IMessenger messenger)
        {
            _codeService = codeService;
            _messenger = messenger;
            
            // Subscribe to deletion messages from Editor
            _messenger.Register<AssetEntityChangedMessage>(this, OnAssetChanged);
        }

        private void OnAssetChanged(object recipient, AssetEntityChangedMessage message)
        {
            if (message.Type == AssetEntityChangedMessage.ChangeType.Deleted)
            {
                // Remove from collections when deleted from editor
                var item = Snippets.FirstOrDefault(s => s.Id == message.Asset?.Id);
                if (item != null)
                {
                    Snippets.Remove(item);
                    _allSnippets.Remove(item);
                }
            }
        }

        [ObservableProperty]
        private bool _isGridLayout = true;

        [RelayCommand]
        private void ToggleLayout()
        {
            IsGridLayout = !IsGridLayout;
        }

        [ObservableProperty]
        private bool _isSelectionMode = false;

        [RelayCommand]
        private void ToggleSelectionMode()
        {
            IsSelectionMode = !IsSelectionMode;
            if (!IsSelectionMode)
            {
                foreach (var s in Snippets) s.IsSelected = false;
            }
        }

        /// <summary>
        /// Reordering allowed only when no filter/search active
        /// </summary>
        public bool CanReorder => string.IsNullOrEmpty(_activeTagFilter) && string.IsNullOrEmpty(_searchQuery);

        #region CRUD Operations

        [RelayCommand]
        private async Task DeleteSelectedAsync(AssetEntity? singleItem = null)
        {
            List<AssetEntity> itemsToDelete;

            if (singleItem != null)
            {
                itemsToDelete = new List<AssetEntity> { singleItem };
            }
            else
            {
                itemsToDelete = Snippets.Where(s => s.IsSelected).ToList();
            }

            if (!itemsToDelete.Any()) return;

            foreach (var item in itemsToDelete)
            {
                Snippets.Remove(item);
                _allSnippets.Remove(item);
                await _codeService.DeleteSnippetAsync(item);
            }
            
            if (Snippets.Count == 0)
            {
                IsSelectionMode = false;
            }
        }

        [RelayCommand]
        private async Task DeleteItemAsync(AssetEntity item)
        {
            if (item == null) return;
            await DeleteSelectedAsync(item);
        }

        #endregion

        #region Loading & Filtering

        public async Task LoadSnippetsAsync()
        {
            var data = await _codeService.GetAllSnippetsAsync();
            if (data != null)
            {
                _allSnippets = data.OrderBy(s => s.SortOrder)
                                   .ThenByDescending(s => s.UpdatedAt)
                                   .ToList();
            }
            else
            {
                _allSnippets = new List<AssetEntity>();
            }
            ApplyFilters();
        }

        partial void OnSearchQueryChanged(string value)
        {
            ApplyFilters();
            OnPropertyChanged(nameof(CanReorder));
        }

        public void FilterByTag(string? tag)
        {
            _activeTagFilter = tag ?? string.Empty;
            ApplyFilters();
            OnPropertyChanged(nameof(CanReorder));
        }

        private void ApplyFilters()
        {
            // Unsubscribe from old collection
            Snippets.CollectionChanged -= OnSnippetsCollectionChanged;
            
            IEnumerable<AssetEntity> query = _allSnippets;

            if (!string.IsNullOrEmpty(_activeTagFilter))
            {
                query = query.Where(s => 
                    s.GetTags().Contains(_activeTagFilter, StringComparer.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                var q = _searchQuery.Trim();
                query = query.Where(s => 
                    (s.FileName != null && s.FileName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (s.ContentIndex != null && s.ContentIndex.Contains(q, StringComparison.OrdinalIgnoreCase))
                );
            }

            Snippets = new ObservableCollection<AssetEntity>(query);
            
            // Subscribe to new collection for reorder detection
            Snippets.CollectionChanged += OnSnippetsCollectionChanged;
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
            // Sync _allSnippets
            _allSnippets = Snippets.ToList();

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
}
