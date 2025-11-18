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
using Pivot.CodeModule.Converters;
using Pivot.CodeModule.Models;
using Pivot.Messages;

namespace Pivot.CodeModule.ViewModels
{
    public partial class CodeViewModel : ObservableObject
    {
        private readonly ICodeRepository? _repo;
        private readonly SnippetCacheService? _snippetCache;
        private List<CodeFile> _allSnippets = new List<CodeFile>();
        // track snippets that were created in-memory and not yet persisted
        private HashSet<Guid> _transientSnippetIds = new HashSet<Guid>();
        private HashSet<string> _selectedCodeTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly ObservableCollection<string> _tagSuggestions = new ObservableCollection<string>();
        private readonly ObservableCollection<TagItem> _currentSnippetTagItems = new ObservableCollection<TagItem>();
        private readonly TagStringToListConverter _tagListConverter = new TagStringToListConverter();

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

        public ObservableCollection<string> TagSuggestions => _tagSuggestions;
        /// <summary>
        /// Provides the current snippet's tags transformed into TagItem badges for immediate UI binding.
        /// Add/Remove flows update this collection before repository saves, keeping cards/scratchpad in sync.
        /// </summary>
        public ObservableCollection<TagItem> CurrentSnippetTagItems => _currentSnippetTagItems;

        partial void OnSelectedSnippetChanging(CodeFile? oldValue, CodeFile? newValue)
        {
            // Do not auto-save while typing or on selection change.
            // Saving occurs explicitly on editor close or when the user triggers Save.
        }

        public CodeViewModel(ICodeRepository repo, SnippetCacheService? snippetCache = null)
        {
            _repo = repo;
            _snippetCache = snippetCache;
            var all = (_repo.GetAll() ?? Enumerable.Empty<CodeFile>()).ToList();
            _allSnippets = all;
            _snippets = new ObservableCollection<CodeFile>(_allSnippets);
            RefreshTagSuggestions();

            // Register for tag selection messages
            try
            {
                var messenger = App.Current.Services.GetService(typeof(IMessenger)) as IMessenger;
                messenger?.Register<CodeViewModel, TagSelectionMessage>(this, (r, m) => r.OnTagSelection(m));
            }
            catch { }

            System.Diagnostics.Debug.WriteLine("CodeViewModel: constructed with repository.");

            // No test seeding in production — snippets come from repository (may be empty)
            if (_snippetCache != null)
            {
                try { _ = _snippetCache.RefreshAsync(_allSnippets); } catch { }
            }
        }

        // Parameterless constructor used as a fallback when DI is unavailable (seeds test items)
        public CodeViewModel()
        {
            _repo = null;
            _snippets = new ObservableCollection<CodeFile>();
            _allSnippets = _snippets.ToList();

            System.Diagnostics.Debug.WriteLine("CodeViewModel: constructed WITHOUT repository (fallback). Saving will be disabled.");

            // No test seeding in fallback; start with an empty collection
            RefreshTagSuggestions();
        }

        // Previously ensured a placeholder "New" item at index 0.
        // This behavior was removed: do not auto-insert a "+ New" placeholder.
        partial void OnSnippetsChanged(ObservableCollection<CodeFile> value)
        {
            // intentionally left blank to avoid inserting placeholder items
        }

        public void Refresh()
        {
            if (_repo == null) return;
            var all = (_repo.GetAll() ?? Enumerable.Empty<CodeFile>()).ToList();
            if (all.Any())
            {
                _allSnippets = all;
                ApplyCodeTagFilters();
                RefreshTagSuggestions();
            }
        }

        // Show snippets that are in Trash (soft-deleted) and still within the restore window.
        public void ShowDeletedSnippets()
        {
            if (_repo == null) return;
            try
            {
                var deleted = (_repo.GetAllDeleted() ?? Enumerable.Empty<CodeFile>()).ToList();
                _allSnippets = deleted;
                ApplyCodeTagFilters();
            }
            catch { }
            if (_snippetCache != null)
            {
                try { await _snippetCache.SaveIfDirtyAsync(); } catch { }
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
                // Mark as transient (in-memory) — only persist when content is provided or explicit save
                try
                {
                    if (newSnippet != null && newSnippet.Id != Guid.Empty)
                    {
                        _transientSnippetIds.Add(newSnippet.Id);
                        System.Diagnostics.Debug.WriteLine("CodeViewModel.AddSnippet: created transient new snippet (not persisted).");
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
            // If snippet is transient and has no content, do not persist — remove it instead
            if (string.IsNullOrWhiteSpace(SelectedSnippet.Content) && _transientSnippetIds.Contains(SelectedSnippet.Id))
            {
                try
                {
                    _transientSnippetIds.Remove(SelectedSnippet.Id);
                    Snippets.Remove(SelectedSnippet);
                    SelectedSnippet = null;
                    IsDirty = false;
                    return;
                }
                catch { }
            }

            await ApplyCachedValuesAsync(SelectedSnippet);
            if (_repo != null)
            {
                // Save synchronously to avoid DbContext concurrent use across threads
                _repo.Save(SelectedSnippet);
                IsDirty = false;
                Refresh();
                // if it was transient, it's now persisted
                try { _transientSnippetIds.Remove(SelectedSnippet.Id); } catch { }
            }
            else
            {
                // No repository available: mark as not dirty but do not persist
                IsDirty = false;
                System.Diagnostics.Debug.WriteLine("CodeViewModel.SaveSnippetAsync: repository is null; Save not performed.");
            }
            if (_snippetCache != null)
            {
                try { await _snippetCache.SaveIfDirtyAsync(); } catch { }
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
                // If this was a transient snippet and there's no content, do not persist — remove it
                if (string.IsNullOrWhiteSpace(file.Content) && _transientSnippetIds.Contains(file.Id))
                {
                    try
                    {
                        _transientSnippetIds.Remove(file.Id);
                        App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                        {
                            try { Snippets.Remove(file); } catch { }
                        });
                    }
                    catch { }
                    return;
                }

                await ApplyCachedValuesAsync(file);
                if (_repo != null)
                {
                    // Save synchronously on calling thread to avoid concurrent DbContext access
                    _repo.Save(file);
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] SaveSnippetFileAsync: saved via repository id={file.Id}");
#endif
                    try { App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => Refresh()); } catch { }
                    // if it was transient, it's now persisted
                    try { _transientSnippetIds.Remove(file.Id); } catch { }
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
            var snippet = SelectedSnippet;
            if (_repo != null)
            {
                _repo.Delete(snippet.Id);
                SelectedSnippet = null;
                Refresh();
                return;
            }

            try
            {
                Snippets.Remove(snippet);
            }
            catch { }
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
            var trimmed = tagName.Trim();
            var current = (snippet.Tags ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (current.Any(t => string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase))) return;

            current.Add(trimmed);
            snippet.Tags = string.Join(",", current);
            UpdateSnippetCache(snippet);
            if (_repo != null)
            {
                await Task.Run(() => _repo.Save(snippet));
            }
            ApplyCodeTagFilters();
            RefreshTagSuggestions();
            if (_snippetCache != null)
            {
                try { await _snippetCache.UpsertAsync(snippet); } catch { }
            }
            RefreshCurrentSnippetTagItems(snippet.Tags);
        }

        public async Task RemoveTagFromSnippetAsync(CodeFile? snippet, string tagName)
        {
            if (snippet == null || string.IsNullOrWhiteSpace(tagName)) return;
            var trimmed = tagName.Trim();
            var current = (snippet.Tags ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            var removed = current.RemoveAll(t => string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase));
            if (removed == 0) return;

            snippet.Tags = string.Join(",", current);
            UpdateSnippetCache(snippet);
            if (_repo != null)
            {
                await Task.Run(() => _repo.Save(snippet));
            }
            ApplyCodeTagFilters();
            RefreshTagSuggestions();
            if (_snippetCache != null)
            {
                try { await _snippetCache.UpsertAsync(snippet); } catch { }
            }
            RefreshCurrentSnippetTagItems(snippet.Tags);
        }

        private void UpdateSnippetCache(CodeFile snippet)
        {
            if (snippet == null) return;
            try
            {
                var existing = _allSnippets.FirstOrDefault(s => s.Id == snippet.Id);
                if (existing != null)
                {
                    existing.Title = snippet.Title;
                    existing.Content = snippet.Content;
                    existing.Tags = snippet.Tags;
                    existing.Language = snippet.Language;
                    existing.Tool = snippet.Tool;
                    existing.Updated = snippet.Updated;
                }
                else
                {
                    _allSnippets.Add(snippet);
                }
            }
            catch { }
        }

        private void RefreshTagSuggestions()
        {
            try
            {
                var names = new List<string>();
                if (_snippetCache != null)
                {
                    try
                    {
                        names.AddRange(_snippetCache.GetAllTagsAsync().GetAwaiter().GetResult());
                    }
                    catch { }
                }
                if (!names.Any())
                {
                    names.AddRange(
                        GetAllTags()
                            .Select(t => t.Name ?? string.Empty)
                            .Where(n => !string.IsNullOrWhiteSpace(n)));
                }
                names = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    try
                    {
                        _tagSuggestions.Clear();
                        foreach (var name in names)
                        {
                            _tagSuggestions.Add(name);
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }

        public void RefreshCurrentSnippetTagItems(string? tags)
        {
            try
            {
                var list = ConvertTags(tags);
                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    try
                    {
                        _currentSnippetTagItems.Clear();
                        foreach (var item in list)
                        {
                            _currentSnippetTagItems.Add(item);
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }

        private IEnumerable<TagItem> ConvertTags(string? tags)
        {
            try
            {
                var result = _tagListConverter.Convert(tags ?? string.Empty, typeof(IEnumerable<TagItem>), null, string.Empty);
                if (result is IEnumerable<TagItem> list) return list;
            }
            catch { }
            return Enumerable.Empty<TagItem>();
        }

        partial void OnSelectedSnippetChanged(CodeFile? oldValue, CodeFile? newValue)
        {
            RefreshCurrentSnippetTagItems(newValue?.Tags);
        }

        private async Task ApplyCachedValuesAsync(CodeFile target)
        {
            if (_snippetCache == null) return;
            try
            {
                var cached = await _snippetCache.GetAsync(target.Id);
                if (cached != null)
                {
                    CopySnippetValues(cached, target);
                }
            }
            catch { }
        }

        private static void CopySnippetValues(CodeFile source, CodeFile target)
        {
            target.Title = source.Title;
            target.Language = source.Language;
            target.Tool = source.Tool;
            target.Tags = source.Tags;
            target.Content = source.Content;
            target.Updated = source.Updated;
            target.IsDeleted = source.IsDeleted;
            target.DeletedAt = source.DeletedAt;
        }
    }
}


