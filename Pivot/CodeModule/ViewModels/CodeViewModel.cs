using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.Input;
using Pivot.CodeModule.Services;
using Pivot.Services;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.CodeModule.Models;
using Pivot.Messages;
using Pivot.Models;
using System.ComponentModel;
using Windows.ApplicationModel.DataTransfer;

namespace Pivot.CodeModule.ViewModels
{
    public partial class CodeViewModel : ObservableObject
    {
        private readonly ICodeRepository? _repo;
        private readonly SnippetCacheService? _snippetCache;
        private readonly SnippetSyncService? _syncService;
        private List<CodeFile> _allSnippets = new List<CodeFile>();
        // track snippets that were created in-memory and not yet persisted
        private HashSet<Guid> _transientSnippetIds = new HashSet<Guid>();
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
        private bool _isCopyToastVisible = false;

        [ObservableProperty]
        private string _newTagName = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Guid> _activeFilters = new ObservableCollection<Guid>();

        public ObservableCollection<TagItem> AvailableTags { get; } = new();
        public ObservableCollection<TagItem> FilteredTags { get; } = new();
        public SnippetSyncService? SyncService => _syncService;
        private string _tagFilterKeyword = string.Empty;
        private volatile bool _isTagSelectionUpdating = false;
        public RelayCommand<TagItem> ToggleTagCommand { get; private set; } = null!;
        public RelayCommand AddTagCommand { get; private set; } = null!;
        partial void OnSelectedSnippetChanging(CodeFile? oldValue, CodeFile? newValue)
        {
            // Do not auto-save while typing or on selection change.
            // Saving occurs explicitly on editor close or when the user triggers Save.
        }

        public CodeViewModel(ICodeRepository repo, SnippetCacheService? snippetCache = null)
        {
            _repo = repo;
            _snippetCache = snippetCache;
            _syncService = new SnippetSyncService(_repo, this);
            var all = (_repo.GetAll() ?? Enumerable.Empty<CodeFile>()).ToList();
            // Remove tags that don't exist in preferences from all snippets
            RemoveInvalidTagsFromSnippets(all);
            _allSnippets = all;
            _snippets = new ObservableCollection<CodeFile>(_allSnippets);
            InitializeTagInfrastructure();

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
            InitializeTagInfrastructure();

            System.Diagnostics.Debug.WriteLine("CodeViewModel: constructed WITHOUT repository (fallback). Saving will be disabled.");
        }

        // Previously ensured a placeholder "New" item at index 0.
        // This behavior was removed: do not auto-insert a "+ New" placeholder.
        partial void OnSnippetsChanged(ObservableCollection<CodeFile> value)
        {
            // intentionally left blank to avoid inserting placeholder items
        }

        partial void OnSelectedSnippetChanged(CodeFile? oldValue, CodeFile? newValue)
        {
            RefreshTagSelection();
        }

        [MemberNotNull(nameof(ToggleTagCommand), nameof(AddTagCommand))]
        private void InitializeTagInfrastructure()
        {
            ToggleTagCommand = new RelayCommand<TagItem>(ToggleTag);
            AddTagCommand = new RelayCommand(AddTag);
            InitializeTags();
        }

        private void InitializeTags()
        {
            var names = CollectTagNames();
            AvailableTags.Clear();
            foreach (var name in names)
            {
                var tag = new TagItem(name);
                AttachTagHandler(tag);
                AvailableTags.Add(tag);
            }
            UpdateFilteredTags();
            RefreshTagSelection();
        }

        private IEnumerable<string> CollectTagNames()
        {
            // Only collect tags that exist in preferences
            // This ensures that tags deleted from preferences are not shown in the UI
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var preferenceTags = CollectPreferenceTagNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            // Only add tags that exist in preferences
            foreach (var name in preferenceTags)
            {
                names.Add(name);
            }
            
            // Also include tags from snippets, but only if they exist in preferences
            foreach (var snippet in _allSnippets)
            {
                foreach (var tag in ParseTagList(snippet.Tags))
                {
                    // Only add tags that exist in preferences
                    if (preferenceTags.Contains(tag))
                    {
                        names.Add(tag);
                    }
                }
            }
            return names.OrderBy(name => name);
        }

        private IEnumerable<string> CollectPreferenceTagNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var repoTags = _repo?.GetAllTags();
                if (repoTags != null)
                {
                    foreach (var tag in repoTags)
                    {
                        var name = (tag?.Name ?? string.Empty).Trim();
                        if (!string.IsNullOrEmpty(name))
                        {
                            names.Add(name);
                        }
                    }
                }
            }
            catch { }

            if (names.Count == 0)
            {
                try
                {
                    var settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
                    var filters = settings?.GetCodeFilters() ?? new List<CustomFilter>();
                    foreach (var filter in filters)
                    {
                        var name = (filter?.Name ?? string.Empty).Trim();
                        if (!string.IsNullOrEmpty(name))
                        {
                            names.Add(name);
                        }
                    }
                }
                catch { }
            }

            return names;
        }

        private void AttachTagHandler(TagItem tag)
        {
            tag.PropertyChanged -= OnTagItemChanged;
            tag.PropertyChanged += OnTagItemChanged;
        }

        public void FilterTags(string keyword)
        {
            _tagFilterKeyword = (keyword ?? string.Empty).Trim().ToLowerInvariant();
            UpdateFilteredTags();
        }

        private void UpdateFilteredTags()
        {
            FilteredTags.Clear();
            var allowedTags = GetAllowedPreferenceTagNames();
            
            // If no preferences are set, show all tags (backward compatibility)
            var shouldFilter = allowedTags.Count > 0;
            
            foreach (var tag in AvailableTags)
            {
                // Filter by keyword
                if (!string.IsNullOrWhiteSpace(_tagFilterKeyword) &&
                    !tag.Name.Contains(_tagFilterKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                // Only show tags that exist in preferences
                if (shouldFilter && !allowedTags.Contains(tag.Name))
                {
                    continue;
                }
                
                FilteredTags.Add(tag);
            }
        }

        private void RefreshTagSelection()
        {
            try
            {
                _isTagSelectionUpdating = true;
                var tags = FilterSnippetTagsByPreferences(SelectedSnippet);
                if (AvailableTags.Count == 0)
                {
                    return;
                }

                foreach (var tag in AvailableTags)
                {
                    var matches = tags.Any(existing => string.Equals(existing, tag.Name, StringComparison.OrdinalIgnoreCase));
                    tag.IsSelected = matches;
                }

                foreach (var tagName in tags)
                {
                    if (!AvailableTags.Any(tag => string.Equals(tag.Name, tagName, StringComparison.OrdinalIgnoreCase)))
                    {
                        var newTag = new TagItem(tagName) { IsSelected = true };
                        AttachTagHandler(newTag);
                        AvailableTags.Add(newTag);
                    }
                }
            }
            finally
            {
                _isTagSelectionUpdating = false;
                UpdateFilteredTags();
            }
        }

        private List<string> FilterSnippetTagsByPreferences(CodeFile? snippet)
        {
            var tags = ParseTagList(snippet?.Tags);
            var allowed = GetAllowedPreferenceTagNames();
            if (allowed.Count == 0) return tags;

            var filtered = tags.Where(tag => allowed.Contains(tag)).ToList();
            if (snippet != null && filtered.Count != tags.Count)
            {
                snippet.Tags = string.Join(", ", filtered);
                snippet.Updated = DateTime.Now;
                _ = PersistSnippetAsync(snippet, refreshAfterSave: false);
            }
            return filtered;
        }

        /// <summary>
        /// Removes tags that don't exist in preferences from all snippets.
        /// This ensures that deleted tags are automatically removed from cards.
        /// </summary>
        private void RemoveInvalidTagsFromSnippets(List<CodeFile> snippets)
        {
            if (snippets == null || !snippets.Any()) return;

            var allowed = GetAllowedPreferenceTagNames();
            // If no preferences are set, don't filter (allow all tags)
            if (allowed.Count == 0) return;

            var updated = false;
            foreach (var snippet in snippets)
            {
                if (string.IsNullOrWhiteSpace(snippet.Tags)) continue;

                var tags = ParseTagList(snippet.Tags);
                var filtered = tags.Where(tag => allowed.Contains(tag)).ToList();
                
                if (filtered.Count != tags.Count)
                {
                    snippet.Tags = filtered.Count > 0 ? string.Join(", ", filtered) : string.Empty;
                    snippet.Updated = DateTime.Now;
                    updated = true;
                    
                    // Persist the change
                    try
                    {
                        if (_repo != null)
                        {
                            _repo.Save(snippet);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"RemoveInvalidTagsFromSnippets: Failed to save snippet {snippet.Id}: {ex.Message}");
                    }
                }
            }

            if (updated)
            {
                System.Diagnostics.Debug.WriteLine("RemoveInvalidTagsFromSnippets: Removed invalid tags from snippets");
            }
        }

        private HashSet<string> GetAllowedPreferenceTagNames()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
                var filters = settings?.GetCodeFilters();
                if (filters != null)
                {
                    foreach (var filter in filters)
                    {
                        var name = (filter?.Name ?? string.Empty).Trim();
                        if (!string.IsNullOrEmpty(name))
                        {
                            result.Add(name);
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        private void ToggleTag(TagItem? tag)
        {
            // No-op: ToggleButton already updates IsSelected via binding,
            // so we avoid flipping it again here to prevent immediate reset.
        }

        private void AddTag()
        {
            var tagName = NormalizeTagName(NewTagName);
            if (string.IsNullOrEmpty(tagName))
            {
                return;
            }

            var existing = AvailableTags.FirstOrDefault(tag =>
                string.Equals(tag.Name, tagName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.IsSelected = true;
            }
            else
            {
                var newTag = new TagItem(tagName)
                {
                    IsSelected = SelectedSnippet != null
                };
                AttachTagHandler(newTag);

                var insertIndex = AvailableTags.TakeWhile(tag =>
                    string.Compare(tag.Name, tagName, StringComparison.OrdinalIgnoreCase) < 0).Count();
                if (insertIndex < 0 || insertIndex > AvailableTags.Count)
                {
                    AvailableTags.Add(newTag);
                }
                else
                {
                    AvailableTags.Insert(insertIndex, newTag);
                }
                try
                {
                    _repo?.AddTag(tagName);
                }
                catch { }
            }

            NewTagName = string.Empty;
            _tagFilterKeyword = string.Empty;
            UpdateFilteredTags();
        }

        private void OnTagItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isTagSelectionUpdating) return;
            if (e.PropertyName != nameof(TagItem.IsSelected)) return;
            if (sender is TagItem tag)
            {
                _ = SyncTagWithSnippetAsync(tag);
            }
        }

        private async Task SyncTagWithSnippetAsync(TagItem tag)
        {
            var snippet = SelectedSnippet;
            if (snippet == null || string.IsNullOrWhiteSpace(tag.Name)) return;
            var tagName = NormalizeTagName(tag.Name);
            if (string.IsNullOrEmpty(tagName)) return;

            var tags = ParseTagList(snippet.Tags);
            var contains = tags.Any(t => string.Equals(t, tagName, StringComparison.OrdinalIgnoreCase));
            if (tag.IsSelected)
            {
                if (!contains)
                {
                    tags.Add(tagName);
                }
            }
            else
            {
                tags.RemoveAll(t => string.Equals(t, tagName, StringComparison.OrdinalIgnoreCase));
            }

            snippet.Tags = string.Join(", ", tags);
            snippet.Updated = DateTime.Now;
            await PersistSnippetAsync(snippet, refreshAfterSave: false);
        }

        private async Task PersistSnippetAsync(CodeFile snippet, bool refreshAfterSave = true)
        {
            if (snippet == null || _repo == null) return;
            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        _repo.Save(snippet);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CodeViewModel.PersistSnippetAsync: Failed to save snippet {snippet.Id}: {ex.Message}");
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CodeViewModel.PersistSnippetAsync: Error persisting snippet: {ex.Message}");
                // Don't refresh if save failed
                return;
            }
            
            if (refreshAfterSave)
            {
                try
                {
                    var dispatcher = App.Current.MainWindow?.DispatcherQueue;
                    if (dispatcher != null)
                    {
                        dispatcher.TryEnqueue(() =>
                        {
                            try
                            {
                                Refresh();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"CodeViewModel.PersistSnippetAsync: Error refreshing after save: {ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CodeViewModel.PersistSnippetAsync: Error scheduling refresh: {ex.Message}");
                }
            }
        }

        private List<string> ParseTagList(string? raw)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return result;
            var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in parts)
            {
                var trimmed = item.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (result.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase))) continue;
                result.Add(trimmed);
            }
            return result;
        }

        private string NormalizeTagName(string? input)
        {
            return (input ?? string.Empty).Trim();
        }

        public void Refresh()
        {
            if (_repo == null) return;
            try
            {
                var all = (_repo.GetAll() ?? Enumerable.Empty<CodeFile>()).ToList();
                if (all.Any())
                {
                    // Remove tags that don't exist in preferences from all snippets
                    RemoveInvalidTagsFromSnippets(all);
                    _allSnippets = all;
                    ApplyCodeTagFilters();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CodeViewModel.Refresh: Error refreshing snippets: {ex.Message}");
                // Keep existing snippets if refresh fails
            }
        }

        // Show snippets that are in Trash (soft-deleted) and still within the restore window.
        public void ShowDeletedSnippets()
        {
            _ = ShowDeletedSnippetsAsync();
        }

        private async Task ShowDeletedSnippetsAsync()
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
            var allSnippets = (_repo.GetAll() ?? Enumerable.Empty<CodeFile>()).ToList();
            if (_activeFilters.Any())
            {
                var filtered = allSnippets.Where(s =>
                {
                    var snippetTags = s.Tags?.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(t => t.Trim().ToLowerInvariant())
                                        .ToList() ?? new List<string>();
                    return _activeFilters.Any(af => snippetTags.Contains(_repo.GetFilterNameById(af).ToLowerInvariant()));
                }).ToList();
                UpdateSnippetsOnUi(filtered);
            }
            else
            {
                UpdateSnippetsOnUi(allSnippets);
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
                    UpdateSnippetsOnUi(_allSnippets);
                    return;
                }

                var filtered = _allSnippets.Where(s =>
                {
                    var snippetTags = (s.Tags ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim()).ToList();
                    // match if any selected tag is present on the snippet
                    return _selectedCodeTags.Any(sel => snippetTags.Any(st => string.Equals(st, sel, StringComparison.OrdinalIgnoreCase)));
                }).ToList();

                UpdateSnippetsOnUi(filtered);
            }
            catch { }
        }

        private void UpdateSnippetsOnUi(IEnumerable<CodeFile> snippetSource)
        {
            var payload = (snippetSource ?? Enumerable.Empty<CodeFile>()).ToList();
            var dispatcher = App.Current.MainWindow?.DispatcherQueue;
            if (dispatcher != null)
            {
                dispatcher.TryEnqueue(() => UpdateVisibleSnippets(payload));
            }
            else
            {
                UpdateVisibleSnippets(payload);
            }
        }

        private void UpdateVisibleSnippets(IReadOnlyList<CodeFile> desiredSnippets)
        {
            if (desiredSnippets == null)
            {
                return;
            }

            // 既存のSelectedSnippetのIDを保存
            var selectedId = SelectedSnippet?.Id ?? Guid.Empty;

            if (Snippets == null)
            {
                Snippets = new ObservableCollection<CodeFile>(desiredSnippets);
                // SelectedSnippetを新しいインスタンスに更新
                if (selectedId != Guid.Empty)
                {
                    var newSelected = Snippets.FirstOrDefault(s => s.Id == selectedId);
                    if (newSelected != null)
                    {
                        SelectedSnippet = newSelected;
                    }
                }
                return;
            }

            var desiredIds = new HashSet<Guid>(desiredSnippets.Select(sn => sn.Id));
            for (int i = Snippets.Count - 1; i >= 0; i--)
            {
                if (!desiredIds.Contains(Snippets[i].Id))
                {
                    Snippets.RemoveAt(i);
                }
            }

            for (int targetIndex = 0; targetIndex < desiredSnippets.Count; targetIndex++)
            {
                var desired = desiredSnippets[targetIndex];
                var existing = Snippets.FirstOrDefault(item => item.Id == desired.Id);
                if (existing == null)
                {
                    Snippets.Insert(targetIndex, desired);
                    continue;
                }

                var currentIndex = Snippets.IndexOf(existing);
                if (currentIndex != targetIndex)
                {
                    Snippets.Move(currentIndex, targetIndex);
                }

                if (!ReferenceEquals(existing, desired))
                {
                    CopySnippetValues(desired, existing);
                }
            }
            
            // SelectedSnippetを新しいインスタンスに更新
            if (selectedId != Guid.Empty)
            {
                var newSelected = Snippets.FirstOrDefault(s => s.Id == selectedId);
                if (newSelected != null && newSelected != SelectedSnippet)
                {
                    SelectedSnippet = newSelected;
                }
            }
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
            var snippet = SelectedSnippet;
            
            // If snippet is transient and has no content, do not persist — remove it instead
            if (string.IsNullOrWhiteSpace(snippet.Content) && _transientSnippetIds.Contains(snippet.Id))
            {
                try
                {
                    _transientSnippetIds.Remove(snippet.Id);
                    Snippets.Remove(snippet);
                    SelectedSnippet = null;
                    IsDirty = false;
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CodeViewModel.SaveSnippetAsync: Error removing transient snippet: {ex.Message}");
                }
            }

            try
            {
                await ApplyCachedValuesAsync(snippet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CodeViewModel.SaveSnippetAsync: Error applying cached values: {ex.Message}");
            }

            if (_repo != null)
            {
                try
                {
                    // Save synchronously to avoid DbContext concurrent use across threads
                    _repo.Save(snippet);
                    IsDirty = false;
                    Refresh();
                    // if it was transient, it's now persisted
                    _transientSnippetIds.Remove(snippet.Id);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CodeViewModel.SaveSnippetAsync: Error saving snippet: {ex.Message}");
                    // Keep IsDirty = true so user can retry
                }
            }
            else
            {
                // No repository available: mark as not dirty but do not persist
                IsDirty = false;
                System.Diagnostics.Debug.WriteLine("CodeViewModel.SaveSnippetAsync: repository is null; Save not performed.");
            }
            
            if (_snippetCache != null)
            {
                try 
                { 
                    await _snippetCache.SaveIfDirtyAsync(); 
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CodeViewModel.SaveSnippetAsync: Error saving cache: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private async Task CopyScratchpadContentAsync()
        {
            var showToast = false;
            try
            {
                var snippet = SelectedSnippet;
                var content = snippet?.Content ?? string.Empty;
                var dp = new DataPackage();
                dp.SetText(content);
                Clipboard.SetContent(dp);
                showToast = true;
                IsCopyToastVisible = true;
                await Task.Delay(1500);
            }
            catch
            {
            }
            finally
            {
                if (showToast)
                {
                    IsCopyToastVisible = false;
                }
            }
        }

        // Save arbitrary snippet (used by host when snippet is closed)
        public async Task SaveSnippetFileAsync(CodeFile file, bool refreshAfterSave = true)
        {
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
                
                // Use sync service for immediate save if available (cancels pending debounced saves)
                if (_syncService != null)
                {
                    _syncService.SaveImmediate(file);
                    if (refreshAfterSave)
                    {
                        try { App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => Refresh()); } catch { }
                    }
                    // if it was transient, it's now persisted
                    try { _transientSnippetIds.Remove(file.Id); } catch { }
                    return;
                }
                
                if (_repo != null)
                {
                    // Save synchronously on calling thread to avoid concurrent DbContext access
                    _repo.Save(file);
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] SaveSnippetFileAsync: saved via repository id={file.Id}");
#endif
                    if (refreshAfterSave)
                    {
                        try { App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => Refresh()); } catch { }
                    }
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
                        if (refreshAfterSave)
                        {
                            try { App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => Refresh()); } catch { }
                        }
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
                            CopySnippetValues(file, existing);
                            
                            // Snippetsコレクション内のインスタンスも更新
                            var inSnippets = Snippets.FirstOrDefault(s => s.Id == file.Id);
                            if (inSnippets != null && !ReferenceEquals(inSnippets, existing))
                            {
                                CopySnippetValues(file, inSnippets);
                            }
                            
                            // SelectedSnippetがSnippetsコレクション内のインスタンスを参照している場合も更新
                            if (SelectedSnippet != null && SelectedSnippet.Id == file.Id)
                            {
                                if (!ReferenceEquals(SelectedSnippet, existing) && !ReferenceEquals(SelectedSnippet, inSnippets))
                                {
                                    CopySnippetValues(file, SelectedSnippet);
                                }
                            }
                            
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
                var results = (_repo.Search(SearchQuery) ?? Enumerable.Empty<CodeFile>()).ToList();
                UpdateSnippetsOnUi(results);
            }
            else
            {
                var q = (SearchQuery ?? string.Empty).ToLowerInvariant();
                var results = _snippets.Where(s => (s.Title ?? string.Empty).ToLowerInvariant().Contains(q) || (s.Content ?? string.Empty).ToLowerInvariant().Contains(q) || (s.Tags ?? string.Empty).ToLowerInvariant().Contains(q)).ToList();
                UpdateSnippetsOnUi(results);
            }
        }

        // Helper: expose repository tags
        public IEnumerable<Pivot.CodeModule.Models.CodeTag> GetAllTags()
        {
            if (_repo == null) return Enumerable.Empty<Pivot.CodeModule.Models.CodeTag>();
            return _repo.GetAllTags();
        }

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

        private async Task ApplyCachedValuesAsync(CodeFile? target)
        {
            if (_snippetCache == null || target == null) return;
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


