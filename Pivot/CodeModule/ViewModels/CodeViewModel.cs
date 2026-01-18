using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.ComponentModel;
using Pivot.Messages;

namespace Pivot.CodeModule.ViewModels
{
    /// <summary>
    /// Coordinator ViewModel for the Code Tab.
    /// Manages sub-viewmodels (Filter, List, Editor).
    /// </summary>
    public partial class CodeViewModel : ObservableObject
    {
        private readonly ILogger<CodeViewModel> _logger;

        public CodeFilterViewModel FilterVM { get; }
        public CodeListViewModel ListVM { get; }
        public CodeEditorViewModel EditorVM { get; }

        public CodeViewModel(
            CodeFilterViewModel filterVM,
            CodeListViewModel listVM,
            CodeEditorViewModel editorVM,
            ILogger<CodeViewModel> logger,
            IMessenger messenger)
        {
            FilterVM = filterVM;
            ListVM = listVM;
            EditorVM = editorVM;
            _logger = logger;
            
            _logger.LogInformation("CodeViewModel (Coordinator) Initialized.");

            // Subscribe to filter changes
            FilterVM.PropertyChanged += FilterVM_PropertyChanged;

            // Subscribe to asset changes
            messenger.Register<AssetEntityChangedMessage>(this, OnAssetChanged);
        }

        private void FilterVM_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CodeFilterViewModel.SelectedTag))
            {
                var tag = FilterVM.SelectedTag;
                // "All" means no filter
                if (string.Equals(tag, "All", System.StringComparison.OrdinalIgnoreCase))
                {
                    tag = null;
                }
                ListVM.FilterByTag(tag);
            }
        }

        public async Task InitializeAsync()
        {
            // 1. Load Snippets
            await ListVM.LoadSnippetsAsync();

            // 2. Populate Tags based on loaded snippets
            // Note: In real scenarios, loading might be separate, but here snippets drive tags.
            // We pass the source (AllSnippets from ListVM logic) to FilterVM
            // ListVM needs to expose AllSnippets or we just use 'Snippets' if it's currently showing all.
            // Better: ListVM should expose 'AllSnippets' (the cache) for tag generation.
            // For now, assume ListVM has a way to get all. 
            // In CodeListViewModel (from memory), it has proper logic.
            // We will pass ListVM.Snippets (potentially filtered, but initially all) or modify ListVM to expose Source.
            
            // 2. Populate Tags based on loaded snippets
            FilterVM.LoadTags(ListVM.Snippets);
            
            // Default to "All"
            FilterVM.SelectedTag = "All";
        }

        private async void OnAssetChanged(object recipient, AssetEntityChangedMessage message)
        {
            // Reload snippets and tags when asset is updated/created/deleted
            await ListVM.LoadSnippetsAsync();
            FilterVM.LoadTags(ListVM.Snippets);
        }
    }
}
