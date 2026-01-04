using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Services;
using Pivot.Models; // AssetEntity
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System;

namespace Pivot.CodeModule.ViewModels
{
    public partial class CodeFilterViewModel : ObservableObject
    {
        private readonly CodeTagService _tagService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private ObservableCollection<string> _tags = new();

        [ObservableProperty]
        private string? _selectedTag;

        public CodeFilterViewModel(CodeTagService tagService, IMessenger messenger)
        {
            _tagService = tagService;
            _messenger = messenger;
        }

        public void LoadTags(IEnumerable<AssetEntity> snippets)
        {
            var availableTags = _tagService.CollectAvailableTags(snippets);
            
            Tags.Clear();
            foreach (var tag in availableTags)
            {
                Tags.Add(tag);
            }
        }
    }
}
