using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using CommunityToolkit.Mvvm.Input;
using Pivot.CodeModule.Models;
using Pivot.CodeModule.Services;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Messages;

namespace Pivot.CodeModule.ViewModels
{
    public partial class CodeViewModel : ObservableObject
    {
        private readonly ICodeRepository? _repo;
        private List<CodeFile> _allSnippets = new List<CodeFile>();
        private HashSet<string> _selectedCodeTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [ObservableProperty]
        private ObservableCollection<CodeFile> _snippets;

        [ObservableProperty]
        private CodeFile? _selectedSnippet;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isDirty = false;

        [ObservableProperty]
        private ObservableCollection<Guid> _activeFilters = new ObservableCollection<Guid>();

        partial void OnSelectedSnippetChanging(CodeFile? oldValue, CodeFile? newValue)
        {
            // Auto-save the old snippet when a new one is selected
            if (oldValue != null && _repo != null && _isDirty)
            {
                Task.Run(() => _repo.Save(oldValue));
            }
        }

        public CodeViewModel(ICodeRepository repo)
        {
            _repo = repo;
            var all = (_repo.GetAll() ?? Enumerable.Empty<CodeFile>()).ToList();
            _allSnippets = all;
            _snippets = new ObservableCollection<CodeFile>(_allSnippets);

            // Register for tag selection messages
            try
            {
                var messenger = App.Current.Services.GetService(typeof(IMessenger)) as IMessenger;
                messenger?.Register<CodeViewModel, TagSelectionMessage>(this, (r, m) => r.OnTagSelection(m));
            }
            catch { }

            // If repository is empty, seed with test snippets for UI layout testing
            if (!_snippets.Any())
            {
                for (int i = 1; i <= 8; i++)
                {
                    _snippets.Add(new CodeFile
                    {
                        Title = $"Test Snippet {i}",
                        Language = i % 2 == 0 ? "Python" : "VEX",
                        Tool = i % 3 == 0 ? "Houdini" : "Unreal",
                        Tags = i % 2 == 0 ? "math,util" : "render,fx",
                        Content = "// sample code...\nprint(\"hello\")",
                        Updated = DateTime.Now.AddMinutes(-i * 5)
                    });
                }
            }
        }

        // Parameterless constructor used as a fallback when DI is unavailable (seeds test items)
        public CodeViewModel()
        {
            _repo = null;
            _snippets = new ObservableCollection<CodeFile>();
            _allSnippets = _snippets.ToList();

            for (int i = 1; i <= 8; i++)
            {
                _snippets.Add(new CodeFile
                {
                    Title = $"Test Snippet {i}",
                    Language = i % 2 == 0 ? "Python" : "VEX",
                    Tool = i % 3 == 0 ? "Houdini" : "Unreal",
                    Tags = i % 2 == 0 ? "math,util" : "render,fx",
                    Content = "// sample code...\nprint(\"hello\")",
                    Updated = DateTime.Now.AddMinutes(-i * 5)
                });
            }
        }

        public void Refresh()
        {
            if (_repo == null) return;
            var all = (_repo.GetAll() ?? Enumerable.Empty<CodeFile>()).ToList();
            if (all.Any())
            {
                _allSnippets = all;
                ApplyCodeTagFilters();
            }
        }

        public void FilterSnippets()
        {
            if (_repo == null) return;
            // This method keeps compatibility with the existing _activeFilters (GUID-based) for other filter types.
            var allSnippets = _repo.GetAll() ?? Enumerable.Empty<CodeFile>();
            if (_activeFilters.Any())
            {
                var filtered = allSnippets.Where(s =>
                {
                    var snippetTags = s.Tags?.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(t => t.Trim().ToLowerInvariant())
                                        .ToList() ?? new List<string>();
                    return _activeFilters.Any(af => snippetTags.Contains(_repo.GetFilterNameById(af).ToLowerInvariant()));
                });
                Snippets = new ObservableCollection<CodeFile>(filtered);
            }
            else
            {
                Snippets = new ObservableCollection<CodeFile>(allSnippets);
            }
        }

        private void OnTagSelection(TagSelectionMessage msg)
        {
            try
            {
                var (tabId, tagName, isSelected) = msg.Value;
                if (!string.Equals(tabId, "Code", StringComparison.OrdinalIgnoreCase)) return;
                if (isSelected) _selectedCodeTags.Add(tagName);
                else _selectedCodeTags.Remove(tagName);
                ApplyCodeTagFilters();
            }
            catch { }
        }

        private void ApplyCodeTagFilters()
        {
            try
            {
                if (_selectedCodeTags == null || !_selectedCodeTags.Any())
                {
                    App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => Snippets = new ObservableCollection<CodeFile>(_allSnippets));
                    return;
                }

                var filtered = _allSnippets.Where(s =>
                {
                    var snippetTags = (s.Tags ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim()).ToList();
                    // match if any selected tag is present on the snippet
                    return _selectedCodeTags.Any(sel => snippetTags.Any(st => string.Equals(st, sel, StringComparison.OrdinalIgnoreCase)));
                }).ToList();

                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => Snippets = new ObservableCollection<CodeFile>(filtered));
            }
            catch { }
        }

        [RelayCommand]
        private void AddSnippet()
        {
            var newSnippet = new CodeFile { Title = "New Snippet", Language = "Python" };
            Snippets.Add(newSnippet);
            SelectedSnippet = newSnippet;
            IsDirty = true;
        }

        [RelayCommand]
        private async Task SaveSnippetAsync()
        {
            if (SelectedSnippet is null) return;
            if (_repo != null)
            {
                await Task.Run(() => _repo.Save(SelectedSnippet));
                IsDirty = false;
                Refresh();
            }
            else
            {
                // No repository available: mark as not dirty but do not persist
                IsDirty = false;
            }
        }

        // Save arbitrary snippet (used by host when snippet is closed)
        public async Task SaveSnippetFileAsync(CodeFile? file)
        {
            if (file is null) return;
            if (_repo != null)
            {
                await Task.Run(() => _repo.Save(file));
            }
        }

        [RelayCommand]
        private void DeleteSnippet()
        {
            if (SelectedSnippet is null) return;
            if (_repo != null)
            {
                _repo.Delete(SelectedSnippet.Id);
            }
            Snippets.Remove(SelectedSnippet);
            SelectedSnippet = null;
        }

        [RelayCommand]
        private void Search()
        {
            if (_repo != null)
            {
                var results = _repo.Search(SearchQuery);
                Snippets = new ObservableCollection<CodeFile>(results);
            }
            else
            {
                var q = (SearchQuery ?? string.Empty).ToLowerInvariant();
                var results = _snippets.Where(s => (s.Title ?? string.Empty).ToLowerInvariant().Contains(q) || (s.Content ?? string.Empty).ToLowerInvariant().Contains(q) || (s.Tags ?? string.Empty).ToLowerInvariant().Contains(q));
                Snippets = new ObservableCollection<CodeFile>(results);
            }
        }
    }
}


