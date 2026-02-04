using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.CodeModule.Services;
using Pivot.CodeModule.Messages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Pivot.CodeModule.ViewModels
{
    /// <summary>
    /// ViewModel for QuickAddControl - manages snippet creation UI state and logic.
    /// </summary>
    public partial class QuickAddViewModel : ObservableObject
    {
        private readonly LanguageDetectionService _languageService;
        private readonly IMessenger _messenger;

        public event EventHandler<QuickAddEventArgs>? SnippetCreated;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _code = string.Empty;

        [ObservableProperty]
        private string _newTag = string.Empty;

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private ObservableCollection<string> _availableTags = new();

        // Track selected tags internally
        private readonly HashSet<string> _selectedTagsSet = new(StringComparer.OrdinalIgnoreCase);

        public QuickAddViewModel(
            LanguageDetectionService languageService,
            IMessenger messenger)
        {
            _languageService = languageService;
            _messenger = messenger;

            // Listen for tags updates from CodeViewModel
            _messenger.Register<TagsUpdatedMessage>(this, OnTagsUpdated);
        }

        private void OnTagsUpdated(object recipient, TagsUpdatedMessage message)
        {
            // Update available tags when tags change globally
            AvailableTags.Clear();
            foreach (var tag in message.Tags.Take(20)) // Limit to 20 for UI performance
            {
                AvailableTags.Add(tag);
            }
        }

        /// <summary>
        /// Called from UI when a tag toggle button is checked/unchecked.
        /// </summary>
        public void ToggleTag(string tag, bool isSelected)
        {
            if (isSelected)
                _selectedTagsSet.Add(tag);
            else
                _selectedTagsSet.Remove(tag);
        }

        [RelayCommand]
        private void AddTag()
        {
            var tag = NewTag?.Trim().TrimStart('#');
            if (string.IsNullOrEmpty(tag)) return;

            // Add to selected tags
            _selectedTagsSet.Add(tag);

            // Add to available tags if not already there
            if (!AvailableTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                AvailableTags.Add(tag);
            }

            // Clear input
            NewTag = string.Empty;

            // Notify UI to check the corresponding toggle button
            // This will be handled by the control finding the tag in AvailableTags
        }

        [RelayCommand]
        private void CreateSnippet()
        {
            var code = Code?.Trim();
            var titleToCheck = Title?.Trim();

            // If both code and title are empty, just collapse (keep existing behavior)
            // If title exists but code is empty, we allow creation (or at least don't clear)
            if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(titleToCheck))
            {
                Clear();
                Collapse();
                return;
            }

            // If code is empty but titleToCheck exists, we treat it as valid.
            // But if implementation requires code, we should just return without clearing.
            if (string.IsNullOrEmpty(code))
            {
                // Don't clear, user might have forgotten to paste code
                return; 
            }

            var title = Title?.Trim() ?? string.Empty;

            // Detect language from extension or content
            var language = _languageService.Detect(title, code);

            // Raise event for parent to handle
            SnippetCreated?.Invoke(this, new QuickAddEventArgs
            {
                Title = title,
                Language = language,
                Code = code,
                Tags = _selectedTagsSet.ToArray()
            });

            // Clear and collapse
            Clear();
            Collapse();
        }

        [RelayCommand]
        private void Clear()
        {
            Title = string.Empty;
            Code = string.Empty;
            NewTag = string.Empty;
            _selectedTagsSet.Clear();
        }

        [RelayCommand]
        private void Expand()
        {
            IsExpanded = true;
        }

        [RelayCommand]
        private void Collapse()
        {
            IsExpanded = false;
        }

        /// <summary>
        /// Public method to trigger save from external keyboard shortcut (Ctrl+S).
        /// </summary>
        public void TrySave()
        {
            if (IsExpanded)
            {
                CreateSnippetCommand.Execute(null);
            }
        }

        /// <summary>
        /// Set available tags from external source (e.g., CodePage initialization).
        /// </summary>
        public void SetAvailableTags(IEnumerable<string> tags)
        {
            AvailableTags.Clear();
            foreach (var tag in tags.Take(20))
            {
                AvailableTags.Add(tag);
            }
        }
    }
}
