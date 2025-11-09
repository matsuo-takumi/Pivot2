using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using CommunityToolkit.Mvvm.Input;
using Pivot.CodeModule.Models;
using Pivot.CodeModule.Services;
using Pivot.Services;
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
            // Do not auto-save while typing or on selection change.
            // Saving occurs explicitly on editor close or when the user triggers Save.
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

            System.Diagnostics.Debug.WriteLine("CodeViewModel: constructed with repository.");

            // No test seeding in production — snippets come from repository (may be empty)
        }

        // Parameterless constructor used as a fallback when DI is unavailable (seeds test items)
        public CodeViewModel()
        {
            _repo = null;
            _snippets = new ObservableCollection<CodeFile>();
            _allSnippets = _snippets.ToList();

            System.Diagnostics.Debug.WriteLine("CodeViewModel: constructed WITHOUT repository (fallback). Saving will be disabled.");

            // No test seeding in fallback; start with an empty collection
        }

        // Ensure a placeholder "New" item is always present at index 0 for UI
        partial void OnSnippetsChanged(ObservableCollection<CodeFile> value)
        {
            try
            {
                if (value == null) return;
                if (!value.Any(s => s.Id == Guid.Empty))
                {
                    var placeholder = new CodeFile { Id = Guid.Empty, Title = "+ New", Content = string.Empty, Updated = DateTime.MinValue };
                    value.Insert(0, placeholder);
                }
            }
            catch { }
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
            try
            {
                if (Snippets != null)
                {
                    // keep placeholder at index 0, insert new snippet after it
                    var insertIndex = Snippets.Count > 0 ? 1 : 0;
                    Snippets.Insert(insertIndex, newSnippet);
                }
                else
                {
                    Snippets = new ObservableCollection<CodeFile> { newSnippet };
                }
                SelectedSnippet = newSnippet;
                IsDirty = true;
                // Persist new snippet immediately if repository available
                try
                {
                    if (_repo != null)
                    {
                        _repo.Save(newSnippet);
                        // refresh internal lists to reflect persisted state
                        Refresh();
                        System.Diagnostics.Debug.WriteLine("CodeViewModel.AddSnippet: saved new snippet via repository.");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("CodeViewModel.AddSnippet: repository is null, new snippet not persisted.");
                    }
                }
                catch { }
            }
            catch { }
        }

        [RelayCommand]
        private async Task SaveSnippetAsync()
        {
            if (SelectedSnippet is null) return;
            if (_repo != null)
            {
                // Save synchronously to avoid DbContext concurrent use across threads
                _repo.Save(SelectedSnippet);
                IsDirty = false;
                Refresh();
            }
            else
            {
                // No repository available: mark as not dirty but do not persist
                IsDirty = false;
                System.Diagnostics.Debug.WriteLine("CodeViewModel.SaveSnippetAsync: repository is null; Save not performed.");
            }
        }

        // Save arbitrary snippet (used by host when snippet is closed)
        public async Task SaveSnippetFileAsync(CodeFile? file)
        {
            if (file is null) return;
            // Debuggable save flow (debug logs only in DEBUG builds)
            try
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[DEBUG] SaveSnippetFileAsync: invoked for id={file.Id}, repoPresent={_repo != null}");
#endif
                if (_repo != null)
                {
                    // Save synchronously on calling thread to avoid concurrent DbContext access
                    _repo.Save(file);
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] SaveSnippetFileAsync: saved via repository id={file.Id}");
#endif
                    try { App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => Refresh()); } catch { }
                }
                else
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] SaveSnippetFileAsync: repository null, using fallback export id={file.Id}");
#endif
                    try
                    {
                        var settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
                        var exportDir = settings?.GetExportOutputDirectory() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(exportDir))
                        {
                            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                            exportDir = System.IO.Path.Combine(docs, "Pivot", "CodeSnippets");
                        }
                        try { if (!System.IO.Directory.Exists(exportDir)) System.IO.Directory.CreateDirectory(exportDir); } catch { }

                        var outPath = System.IO.Path.Combine(exportDir, file.Id.ToString() + ".json");
                        var json = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            file.Id,
                            file.Title,
                            file.Language,
                            file.Tool,
                            file.Tags,
                            file.Content,
                            file.Updated
                        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        System.IO.File.WriteAllText(outPath, json);
#if DEBUG
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] SaveSnippetFileAsync: fallback exported json to '{outPath}'");
#endif
                        try { App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => Refresh()); } catch { }
                    }
                    catch
                    {
#if DEBUG
                        System.Diagnostics.Debug.WriteLine("[DEBUG] SaveSnippetFileAsync: fallback export failed");
#endif
                    }
                }
            }
            catch
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("[DEBUG] SaveSnippetFileAsync: unexpected save error");
#endif
            }
            // Ensure UI list reflects saved changes immediately
            try
            {
                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    try
                    {
                        var existing = _allSnippets.FirstOrDefault(s => s.Id == file.Id);
                        if (existing != null)
                        {
                            existing.Title = file.Title;
                            existing.Content = file.Content;
                            existing.Tags = file.Tags;
                            existing.Language = file.Language;
                            existing.Tool = file.Tool;
                            existing.Updated = file.Updated;
                            // Refresh visible collection
                            ApplyCodeTagFilters();
                        }
                        else
                        {
                            // Insert after placeholder if present
                            if (_allSnippets == null) _allSnippets = new List<CodeFile>();
                            var insertIndex = _allSnippets.Count > 0 && _allSnippets[0].Id == Guid.Empty ? 1 : 0;
                            _allSnippets.Insert(insertIndex, file);
                            ApplyCodeTagFilters();
                        }
                    }
                    catch { }
                });
            }
            catch { }
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

        // Helper: expose repository tags
        public IEnumerable<Pivot.CodeModule.Models.CodeTag> GetAllTags()
        {
            if (_repo == null) return Enumerable.Empty<Pivot.CodeModule.Models.CodeTag>();
            return _repo.GetAllTags();
        }

        // Helper: get snippets that contain a given tag name
        public IEnumerable<CodeFile> GetSnippetsByTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return Enumerable.Empty<CodeFile>();
            var all = _repo != null ? (_repo.GetAll() ?? Enumerable.Empty<CodeFile>()) : _allSnippets.AsEnumerable();
            var matches = all.Where(s =>
            {
                var tags = (s.Tags ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim());
                return tags.Any(t => string.Equals(t, tagName, StringComparison.OrdinalIgnoreCase));
            });
            return matches;
        }

        // Helper: attach a tag to a snippet and persist
        public async Task AddTagToSnippetAsync(CodeFile? snippet, string tagName)
        {
            if (snippet == null || string.IsNullOrWhiteSpace(tagName)) return;
            var current = (snippet.Tags ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
            if (!current.Contains(tagName, StringComparer.OrdinalIgnoreCase))
            {
                current.Add(tagName.Trim());
                snippet.Tags = string.Join(",", current);
                if (_repo != null)
                {
                    _repo.Save(snippet);
                }
            }
        }
    }
}


