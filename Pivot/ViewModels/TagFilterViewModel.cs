using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Pivot.Models;
using Pivot.Services;

namespace Pivot.ViewModels
{
    public partial class TagFilterViewModel : ObservableObject
    {
        private readonly TagFilterService _tagService;
        private readonly ILogger<TagFilterViewModel> _logger;

        public ObservableCollection<TagDefinition> AvailableTags { get; } = new ObservableCollection<TagDefinition>();
        public ObservableCollection<string> SelectedTagNames { get; } = new ObservableCollection<string>();

        public event Action? SelectedTagsChanged;

        public TagFilterViewModel(TagFilterService tagService, ILogger<TagFilterViewModel> logger)
        {
            _tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
            _logger = logger;

            SelectedTagNames.CollectionChanged += SelectedTagNames_CollectionChanged;
        }

        private void SelectedTagNames_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            try
            {
                SelectedTagsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TagFilterViewModel: Error while raising SelectedTagsChanged");
            }
        }

        public async Task LoadAvailableTagsAsync()
        {
            try
            {
                await _tagService.EnsureDefaultsAsync();
                var tags = _tagService.GetAvailableTags();
                AvailableTags.Clear();
                foreach (var t in tags)
                {
                    AvailableTags.Add(t);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TagFilterViewModel: Failed to load tags");
            }
        }

        public async Task AddTagAsync(string name, string extensionsCsv, string? colorHex = null)
        {
            var tag = new TagDefinition
            {
                Name = (name ?? string.Empty).Trim(),
                Extensions = ParseExtensions(extensionsCsv),
                ColorHex = colorHex
            };
            await _tagService.AddTagAsync(tag);
            await LoadAvailableTagsAsync();
        }

        public async Task UpdateTagAsync(string originalName, string newName, string extensionsCsv, string? colorHex = null)
        {
            var tag = new TagDefinition
            {
                Name = (newName ?? string.Empty).Trim(),
                Extensions = ParseExtensions(extensionsCsv),
                ColorHex = colorHex
            };
            await _tagService.UpdateTagAsync(originalName, tag);
            await LoadAvailableTagsAsync();
        }

        [RelayCommand]
        public async Task DeleteTagAsync(string name)
        {
            await _tagService.DeleteTagAsync(name);
            // remove from selection if present
            if (SelectedTagNames.Contains(name)) SelectedTagNames.Remove(name);
            await LoadAvailableTagsAsync();
        }

        [RelayCommand]
        public void ToggleTag(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (SelectedTagNames.Contains(name)) SelectedTagNames.Remove(name);
            else SelectedTagNames.Add(name);
        }

        public HashSet<string> GetSelectedExtensions()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var selected = SelectedTagNames.ToList();
            foreach (var t in AvailableTags.Where(a => selected.Contains(a.Name)))
            {
                foreach (var ext in t.Extensions)
                {
                    var cleaned = (ext ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
                    if (!string.IsNullOrEmpty(cleaned)) set.Add(cleaned);
                }
            }
            return set;
        }

        private List<string> ParseExtensions(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
            return csv.Split(',')
                .Select(s => s.Trim().TrimStart('.').ToLowerInvariant())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
