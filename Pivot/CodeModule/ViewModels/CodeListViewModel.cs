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

        [RelayCommand]
        private async Task DeleteSelectedAsync(AssetEntity? singleItem = null)
        {
            // If a single item is passed (from context menu), delete just that item
            if (singleItem != null)
            {
                await DeleteItemInternalAsync(singleItem);
                return;
            }
            
            // Otherwise delete all selected items
            var selected = Snippets.Where(s => s.IsSelected).ToList();
            if (!selected.Any()) return;

            foreach (var item in selected)
            {
                await DeleteItemInternalAsync(item);
            }
            IsSelectionMode = false;
        }

        [RelayCommand]
        private async Task DeleteItemAsync(AssetEntity item)
        {
            if (item == null) return;
            await DeleteItemInternalAsync(item);
        }
        
        private async Task DeleteItemInternalAsync(AssetEntity item)
        {
            await _codeService.DeleteSnippetAsync(item);
            Snippets.Remove(item);
            _allSnippets.Remove(item);
        }

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
        }

        public void FilterByTag(string? tag)
        {
            _activeTagFilter = tag ?? string.Empty;
            ApplyFilters();
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

            // 2. Search Query (Title or ContentIndex)
            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                var q = _searchQuery.Trim();
                query = query.Where(s => 
                    (s.FileName != null && s.FileName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (s.ContentIndex != null && s.ContentIndex.Contains(q, StringComparison.OrdinalIgnoreCase))
                );
            }

            Snippets = new ObservableCollection<AssetEntity>(query);
        }
        
        [RelayCommand]
        private async Task MoveItemUpAsync(AssetEntity item)
        {
            if (item == null || !_allSnippets.Contains(item)) return;

            var index = Snippets.IndexOf(item);
            if (index > 0)
            {
                var prevItem = Snippets[index - 1];
                
                // Swap SortOrder
                // If SortOrder is 0, we might need to initialize them
                // Simple logic: Swap SortOrder. BUT if they are equal, we need to distinguish.
                // Better approach: Assign new SortOrders based on current view index.
                
                // For simple "Swap":
                // If SortOrders are same, decrement target item's SortOrder.
                
                int itemOrder = item.SortOrder;
                int prevOrder = prevItem.SortOrder;
                
                if (itemOrder == prevOrder)
                {
                    // If equal, force prevItem to be higher (larger value? Sort is Ascending or Descending?)
                    // CodeListViewModel sorts by SortOrder (Ascending presumed from OrderBy(s => s.SortOrder))
                    // So to move UP (visual up, index 0), SortOrder must be SMALLER.
                    item.SortOrder = prevOrder - 1;
                }
                else
                {
                    item.SortOrder = prevOrder;
                    prevItem.SortOrder = itemOrder;
                }

                // Update UI Collection (Swap)
                Snippets.Move(index, index - 1);

                // Save both
                await _codeService.SaveSnippetAsync(item);
                await _codeService.SaveSnippetAsync(prevItem);
            }
        }

        [RelayCommand]
        private async Task MoveItemDownAsync(AssetEntity item)
        {
            if (item == null || !_allSnippets.Contains(item)) return;

            var index = Snippets.IndexOf(item);
            if (index < Snippets.Count - 1)
            {
                var nextItem = Snippets[index + 1];

                // Swap SortOrder
                // To move DOWN (visual down, index increase), SortOrder must be LARGER.
                
                int itemOrder = item.SortOrder;
                int nextOrder = nextItem.SortOrder;

                if (itemOrder == nextOrder)
                {
                    item.SortOrder = nextOrder + 1;
                }
                else
                {
                    item.SortOrder = nextOrder;
                    nextItem.SortOrder = itemOrder;
                }

                // Update UI Collection
                Snippets.Move(index, index + 1);

                // Save both
                await _codeService.SaveSnippetAsync(item);
                await _codeService.SaveSnippetAsync(nextItem);
            }
        }

        /// <summary>
        /// Move an item to a specific index via drag-and-drop
        /// </summary>
        public async Task MoveItemToIndexAsync(int itemId, int targetIndex)
        {
            // Find the item by ID
            var item = Snippets.FirstOrDefault(s => s.Id == itemId);
            if (item == null) return;

            int currentIndex = Snippets.IndexOf(item);
            if (currentIndex == targetIndex || currentIndex < 0) return;

            // Adjust target index if moving down (since removal shifts indices)
            if (currentIndex < targetIndex)
            {
                targetIndex--;
            }

            // Clamp target index
            targetIndex = Math.Max(0, Math.Min(targetIndex, Snippets.Count - 1));
            if (currentIndex == targetIndex) return;

            // Move in the observable collection
            Snippets.Move(currentIndex, targetIndex);

            // Recalculate SortOrder for all items based on their new positions
            for (int i = 0; i < Snippets.Count; i++)
            {
                Snippets[i].SortOrder = i;
            }

            // Update _allSnippets to reflect the new order
            var movedItem = _allSnippets.FirstOrDefault(s => s.Id == itemId);
            if (movedItem != null)
            {
                _allSnippets.Remove(movedItem);
                int allSnippetsTargetIndex = Math.Min(targetIndex, _allSnippets.Count);
                _allSnippets.Insert(allSnippetsTargetIndex, movedItem);
            }

            // Save all items with updated SortOrder
            foreach (var snippet in Snippets)
            {
                await _codeService.SaveSnippetAsync(snippet);
            }
        }
    }
}
