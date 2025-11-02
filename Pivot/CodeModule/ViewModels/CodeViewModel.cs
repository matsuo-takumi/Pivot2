using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using CommunityToolkit.Mvvm.Input;
using Pivot.CodeModule.Models;
using Pivot.CodeModule.Services;

namespace Pivot.CodeModule.ViewModels
{
    public partial class CodeViewModel : ObservableObject
    {
        private readonly ICodeRepository? _repo;

        [ObservableProperty]
        private ObservableCollection<CodeFile> _snippets;

        [ObservableProperty]
        private CodeFile? _selectedSnippet;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isDirty = false;

        public CodeViewModel(ICodeRepository repo)
        {
            _repo = repo;
            var all = _repo.GetAll() ?? Enumerable.Empty<CodeFile>();
            _snippets = new ObservableCollection<CodeFile>(all);

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
            var all = _repo.GetAll() ?? Enumerable.Empty<CodeFile>();
            // Only replace the in-memory snippets if repository actually contains items.
            // This avoids wiping out seeded test items during early UI actions.
            if (all.Any())
            {
                Snippets = new ObservableCollection<CodeFile>(all);
            }
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


