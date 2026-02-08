using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.ComponentModel;
using Pivot.Messages;
using Pivot.Engine.Messages;
using Pivot.Engine.Models;
using Pivot.Models;
using Pivot.CodeModule.Messages;

namespace Pivot.CodeModule.ViewModels
{
    /// <summary>
    /// Coordinator ViewModel for the Code Tab.
    /// Manages sub-viewmodels (Filter, List, Editor).
    /// </summary>
    public partial class CodeViewModel : ObservableObject
    {
        private readonly ILogger<CodeViewModel> _logger;
        private readonly IMessenger _messenger;

        public CodeFilterViewModel FilterVM { get; }
        public CodeListViewModel ListVM { get; }
        public CodeEditorViewModel EditorVM { get; }
        public CodeSettingsViewModel CodePreferences { get; }

        public CodeViewModel(
            CodeFilterViewModel filterVM,
            CodeListViewModel listVM,
            CodeEditorViewModel editorVM,
            CodeSettingsViewModel codeSettingsVM,
            ILogger<CodeViewModel> logger,
            IMessenger messenger)
        {
            FilterVM = filterVM;
            ListVM = listVM;
            EditorVM = editorVM;
            CodePreferences = codeSettingsVM;
            _logger = logger;
            _messenger = messenger;
            
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
            // 1. Load Snippets (Fast, paginated)
            await ListVM.LoadSnippetsAsync();

            // 2. Populate Tags (Async, optimized)
            await FilterVM.LoadTagsAsync();
            
            if (string.IsNullOrEmpty(FilterVM.SelectedTag))
            {
                FilterVM.SelectedTag = null;
            }
        }

        private async void OnAssetChanged(object recipient, AssetEntityChangedMessage message)
        {
            // Reload snippets and tags when asset is updated/created/deleted
            await ListVM.LoadSnippetsAsync();
            await FilterVM.LoadTagsAsync();
        }

        /// <summary>
        /// Handles snippet creation from QuickAdd control.
        /// Orchestrates snippet creation, data reload, and tag updates.
        /// </summary>
        public async Task OnSnippetCreatedAsync(QuickAddEventArgs args)
        {
            var newSnippet = await EditorVM.CreateSnippetAsync(
                args.Title,
                args.Language,
                args.Code,
                args.Tags);

            if (newSnippet != null)
            {
                // Reload data in parallel
                await Task.WhenAll(
                    ListVM.LoadSnippetsAsync(),
                    FilterVM.LoadTagsAsync()
                );

                // Notify QuickAdd to update its available tags
                _messenger.Send(new TagsUpdatedMessage(FilterVM.Tags));
            }
        }
    }
}
