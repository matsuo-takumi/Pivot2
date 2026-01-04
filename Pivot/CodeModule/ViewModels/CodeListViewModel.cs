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

        public async Task LoadSnippetsAsync()
        {
            var data = await _codeService.GetAllSnippetsAsync();
            _allSnippets = data ?? new List<AssetEntity>();
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
        
        partial void OnSelectedSnippetChanged(AssetEntity? value)
        {
            // Notify EditorVM via Messenger or Parent Coordinator?
            // Messenger is decoupled. 
            // We can send a generic AssetSelectionMessage or specific CodeSelectionMessage.
            // For now, let's assume Coordinator (CodeViewModel) observes this property or we send a message.
            // Sending message is cleanest for separation.
            // Or EditorVM subscribes to this VM? No.
            
            // Let's implement "CodeSnippetSelectedMessage" later if needed.
            // For now, just property change.
        }
    }
}
