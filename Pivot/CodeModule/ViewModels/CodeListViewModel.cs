using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Models; // AssetEntity
using Pivot.Services;
using Pivot.Messages;
using System.Collections.ObjectModel;
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

        // Full dataset
        private List<AssetEntity> _allSnippets = new();

        [ObservableProperty]
        private ObservableCollection<AssetEntity> _snippets = new();

        [ObservableProperty]
        private AssetEntity? _selectedSnippet;

        // Current filter state
        private string _activeTagFilter = string.Empty;
        [ObservableProperty]
        private string _searchQuery = string.Empty;

        public CodeListViewModel(CodeService codeService, IMessenger messenger)
        {
            _codeService = codeService;
            _messenger = messenger;
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
                // Clear selection when exiting mode
                foreach (var s in Snippets) s.IsSelected = false;
            }
        }

        public bool IsReorderingAllowed => string.IsNullOrEmpty(_activeTagFilter) && string.IsNullOrEmpty(_searchQuery);

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
                // Create a copy of the list to avoid collection modification exceptions
                itemsToDelete = Snippets.Where(s => s.IsSelected).ToList();
            }

            if (!itemsToDelete.Any()) return;

            foreach (var item in itemsToDelete)
            {
                // Remove from UI first for responsiveness
                Snippets.Remove(item);
                _allSnippets.Remove(item);
                
                // Then remove from DB
                await _codeService.DeleteSnippetAsync(item);
            }
            
            // Clear selection mode if empty
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
        
        // Removed internal helper to keep logic centralized in DeleteSelectedAsync
        // private async Task DeleteItemInternalAsync...

        public async Task LoadSnippetsAsync()
        {
            var data = await _codeService.GetAllSnippetsAsync();
            if (data != null)
            {
                // Sort by SortOrder, then UpdatedAt
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
            OnPropertyChanged(nameof(IsReorderingAllowed));
        }

        public void FilterByTag(string? tag)
        {
            _activeTagFilter = tag ?? string.Empty;
            ApplyFilters();
            OnPropertyChanged(nameof(IsReorderingAllowed));
        }

        private void ApplyFilters()
        {
            IEnumerable<AssetEntity> query = _allSnippets;

            // 1. Tag Filter
            if (!string.IsNullOrEmpty(_activeTagFilter))
            {
                query = query.Where(s => 
                    s.GetTags().Contains(_activeTagFilter, StringComparer.OrdinalIgnoreCase));
            }

            // 2. Search Query
            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                var q = _searchQuery.Trim();
                query = query.Where(s => 
                    (s.FileName != null && s.FileName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (s.ContentIndex != null && s.ContentIndex.Contains(q, StringComparison.OrdinalIgnoreCase))
                );
            }

            // Always creating a new collection is safer for ItemsRepeater reset
            Snippets = new ObservableCollection<AssetEntity>(query);
        }
        
        [RelayCommand]
        private async Task MoveItemUpAsync(AssetEntity item)
        {
            // Only allow when not filtered
            if (!IsReorderingAllowed) return;
            
            var index = Snippets.IndexOf(item);
            if (index > 0)
            {
                await MoveItemToIndexAsync(item.Id, index - 1);
            }
        }

        [RelayCommand]
        private async Task MoveItemDownAsync(AssetEntity item)
        {
            if (!IsReorderingAllowed) return;

            var index = Snippets.IndexOf(item);
            if (index < Snippets.Count - 1)
            {
                await MoveItemToIndexAsync(item.Id, index + 1);
            }
        }

        /// <summary>
        /// Moves the item visually in the collection without saving.
        /// Used for real-time drag-and-drop feedback.
        /// </summary>
        public void MoveItemVisual(int itemId, int targetIndex)
        {
            if (!IsReorderingAllowed) return;

            var item = Snippets.FirstOrDefault(s => s.Id == itemId);
            if (item == null) return;

            int currentIndex = Snippets.IndexOf(item);
            if (currentIndex == -1 || currentIndex == targetIndex) return;

            // Adjust target index if moving down (since removal shifts indices)
            if (currentIndex < targetIndex)
            {
                targetIndex--;
            }

            // Clamp
            targetIndex = Math.Clamp(targetIndex, 0, Snippets.Count - 1);

            // Move in observable collection (Visual update)
            Snippets.Move(currentIndex, targetIndex);
        }

        /// <summary>
        /// Commits the current order to the database.
        /// Call this on Drop.
        /// </summary>
        public async Task CommitReorderAsync()
        {
            // Sync _allSnippets to match the new UI order
            // Since we only reorder when not filtered, Snippets contains all items.
            // We can just rebuild _allSnippets from Snippets.
            // (Or sort _allSnippets matching Snippets IDs)
            
            // Reconstruct _allSnippets
            _allSnippets = Snippets.ToList();

            // Batch Update SortOrders
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
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Old single-shot move method (kept for backward compatibility if needed, using new components)
        /// </summary>
        public async Task MoveItemToIndexAsync(int itemId, int targetIndex)
        {
            MoveItemVisual(itemId, targetIndex);
            await CommitReorderAsync();
        }
    }
}
