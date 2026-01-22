using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Services;
using Pivot.Models; // AssetEntity
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace Pivot.CodeModule.ViewModels
{
    public partial class CodeFilterViewModel : ObservableObject
    {
        private readonly CodeTagService _tagService;
        private readonly CodeService _codeService;
        private readonly AssetQueryService _queryService;
        private readonly IDialogService _dialogService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private ObservableCollection<string> _tags = new();

        [ObservableProperty]
        private string? _selectedTag;

        public CodeFilterViewModel(
            CodeTagService tagService, 
            CodeService codeService,
            AssetQueryService queryService,
            IDialogService dialogService,
            IMessenger messenger)
        {
            _tagService = tagService;
            _codeService = codeService;
            _queryService = queryService;
            _dialogService = dialogService;
            _messenger = messenger;
        }

        public async Task LoadTagsAsync()
        {
            var tags = await _queryService.GetUniqueTagsAsync(AssetKind.Code);
            var preferenceTags = _tagService.GetAllowedPreferenceTags();
            
            var allTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in preferenceTags) allTags.Add(t);
            foreach (var t in tags) allTags.Add(t);

            var sorted = allTags.OrderBy(t => t).ToList();

            Tags.Clear();
            Tags.Add("All");
            foreach (var tag in sorted)
            {
                Tags.Add(tag);
            }
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private async Task DeleteTagAsync(string tagName)
        {
            if (!IsTagDeletable(tagName))
            {
                return;
            }

            // Confirm deletion
            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Delete Tag", 
                $"Are you sure you want to delete tag '{tagName}'?\nThis will remove it from all snippets.");
            
            if (!confirmed) return;

            // Execute deletion
            await _codeService.RemoveTagGloballyAsync(tagName);

            // Notify to reload data (snippets and tags)
            // Passing null asset signals general update or we rely on CodeViewModel reloading blindly.
            _messenger.Send(new Pivot.Messages.AssetEntityChangedMessage(
                null, 
                string.Empty, 
                Pivot.Messages.AssetEntityChangedMessage.ChangeType.Updated));
        }

        public bool IsTagDeletable(string? tagName)
        {
            return !string.IsNullOrWhiteSpace(tagName) && 
                   !string.Equals(tagName, "All", StringComparison.OrdinalIgnoreCase);
        }
    }
}
