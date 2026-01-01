using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.Input;
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
        private readonly CodeService? _codeService;
        private readonly FilterSettingsService? _filterSettings;
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
        private bool _isCopyToastVisible = false;

        [ObservableProperty]
        private string _newTagName = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Guid> _activeFilters = new ObservableCollection<Guid>();

        public ObservableCollection<TagItem> AvailableTags { get; } = new();
        public ObservableCollection<TagItem> FilteredTags { get; } = new();
        
        /// <summary>
        /// Navigation items for the left pane. Built from preferences and updated when filters change.
        /// </summary>
        public ObservableCollection<NavigationItem> NavigationItems { get; } = new();
        
        [ObservableProperty]
        private NavigationItem? _selectedNavigationItem;
        
        private string _tagFilterKeyword = string.Empty;
        private volatile bool _isTagSelectionUpdating = false;
        public RelayCommand<TagItem> ToggleTagCommand { get; private set; } = null!;
        public RelayCommand AddTagCommand { get; private set; } = null!;
        public RelayCommand ShowAllSnippetsCommand { get; private set; } = null!;
        public RelayCommand ShowDeletedSnippetsCommand { get; private set; } = null!;
        public RelayCommand<Guid> SelectFilterCommand { get; private set; } = null!;
        public RelayCommand<NavigationItem> NavigateToItemCommand { get; private set; } = null!;
        
        partial void OnSelectedSnippetChanging(CodeFile? oldValue, CodeFile? newValue)
        {
            // Do not auto-save while typing or on selection change.
            // Saving occurs explicitly on editor close or when the user triggers Save.
        }

        /// <summary>
        /// Main constructor using CodeService (new architecture).
        /// </summary>
        public CodeViewModel(CodeService codeService, FilterSettingsService filterSettings)
        {
            _codeService = codeService;
            _filterSettings = filterSettings;
            _snippets = new ObservableCollection<CodeFile>();
            InitializeTagInfrastructure();

            // Register for tag selection messages
            try
            {
                var messenger = App.Current.Services.GetService(typeof(IMessenger)) as IMessenger;
                messenger?.Register<CodeViewModel, TagSelectionMessage>(this, (r, m) => r.OnTagSelection(m));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CodeViewModel: Error registering messenger: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine("CodeViewModel: constructed with CodeService.");

            // Initial load
            _ = RefreshAsync();
        }

        // Parameterless constructor used as a fallback when DI is unavailable
        public CodeViewModel()
        {
            _codeService = null;
            _filterSettings = null;
            _snippets = new ObservableCollection<CodeFile>();
            _allSnippets = _snippets.ToList();
            InitializeTagInfrastructure();

            System.Diagnostics.Debug.WriteLine("CodeViewModel: constructed WITHOUT CodeService (fallback).");
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

        [ObservableProperty]
        private bool _isTrashMode = false;

        [MemberNotNull(nameof(ToggleTagCommand), nameof(AddTagCommand), nameof(ShowAllSnippetsCommand), nameof(ShowDeletedSnippetsCommand), nameof(SelectFilterCommand), nameof(NavigateToItemCommand), nameof(EmptyTrashCommand))]
        private void InitializeTagInfrastructure()
        {
            ToggleTagCommand = new RelayCommand<TagItem>(ToggleTag);
            AddTagCommand = new RelayCommand(AddTag);
            ShowAllSnippetsCommand = new RelayCommand(ExecuteShowAllSnippets);
            ShowDeletedSnippetsCommand = new RelayCommand(() => ShowDeletedSnippets());
            SelectFilterCommand = new RelayCommand<Guid>(ExecuteSelectFilter);
            NavigateToItemCommand = new RelayCommand<NavigationItem>(ExecuteNavigateToItem);
            EmptyTrashCommand = new RelayCommand(ExecuteEmptyTrash);
            InitializeTags();
            BuildNavigationItems();
        }

        public RelayCommand EmptyTrashCommand { get; private set; } = null!;

        private void ExecuteEmptyTrash()
        {
            if (_codeService == null) return;
            
            // Get all logical deleted snippets from _allSnippets
            var trash = _allSnippets.Where(x => x.IsDeleted).ToList();
            if (!trash.Any()) return;

            foreach (var snippet in trash)
            {
                // Hard delete via CodeService
                try 
                {
                    _ = _codeService.HardDeleteSnippetAsync(snippet);
                    
                    // Remove from internal list
                    _allSnippets.Remove(snippet);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deleting snippet {snippet.Id}: {ex.Message}");
                }
            }

            // Refresh UI
            ShowDeletedSnippets();
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
                // Use the local GetAllTags() method which extracts tags from _allSnippets
                var repoTags = GetAllTags();
                foreach (var tag in repoTags)
                {
                    var name = (tag?.Name ?? string.Empty).Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                        names.Add(name);
                    }
                }
            }
            catch { }

            if (names.Count == 0)
            {
                try
                {
                    var filterSettings = App.Current.Services.GetService(typeof(FilterSettingsService)) as FilterSettingsService;
                    var filters = filterSettings?.GetCodeFilters() ?? new List<CustomFilter>();
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
                        if (_codeService != null)
                        {
                            _ = _codeService.SaveSnippetAsync(snippet);
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
                var filterSettings = App.Current.Services.GetService(typeof(FilterSettingsService)) as FilterSettingsService;
                var filters = filterSettings?.GetCodeFilters();
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
                // Tags are now managed via snippet's Tags property
                // No need for separate tag table in new architecture
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
            if (snippet == null || _codeService == null) return;
            try
            {
                await _codeService.SaveSnippetAsync(snippet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CodeViewModel.PersistSnippetAsync: Error persisting snippet: {ex.Message}");
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

        #region Navigation Logic (moved from CodePage.xaml.cs)

        /// <summary>
        /// Builds navigation items from preferences. Call this to refresh the left navigation menu.
        /// </summary>
        public void BuildNavigationItems()
        {
            try
            {
                NavigationItems.Clear();

                // Add "All Snippets" item
                var allItem = NavigationItem.CreateAllSnippets();
                NavigationItems.Add(allItem);

                // Get filters from settings
                var filterSettings = App.Current.Services.GetService(typeof(FilterSettingsService)) as FilterSettingsService;
                var filters = filterSettings?.GetCodeFilters() ?? new List<CustomFilter>();

                foreach (var f in filters.OrderBy(f => f.SortOrder).ThenBy(f => f.Name))
                {
                    try
                    {
                        var navItem = NavigationItem.CreateFilter(f.Name, f.Id);
                        NavigationItems.Add(navItem);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"BuildNavigationItems: Error adding filter '{f.Name}': {ex.Message}");
                    }
                }

                // Select "All Snippets" by default
                SelectedNavigationItem = allItem;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BuildNavigationItems: Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes navigation items. Used when filters are updated in preferences.
        /// </summary>
        public void RefreshNavigationItems()
        {
            BuildNavigationItems();
        }

        private void ExecuteShowAllSnippets()
        {
            try
            {
                ActiveFilters.Clear();
                _selectedCodeTags.Clear();
                IsTrashMode = false;
                Refresh();
                SelectedSnippet = null;
                System.Diagnostics.Debug.WriteLine("ExecuteShowAllSnippets: Showing all snippets");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExecuteShowAllSnippets: Error: {ex.Message}");
            }
        }

        private void ExecuteSelectFilter(Guid filterId)
        {
            try
            {
                ActiveFilters.Clear();
                if (filterId != Guid.Empty)
                {
                    ActiveFilters.Add(filterId);
                }
                IsTrashMode = false;
                FilterSnippets();
                SelectedSnippet = null;
                System.Diagnostics.Debug.WriteLine($"ExecuteSelectFilter: Selected filter {filterId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExecuteSelectFilter: Error: {ex.Message}");
            }
        }

        private void ExecuteNavigateToItem(NavigationItem? item)
        {
            if (item == null) return;

            try
            {
                if (item.IsAllSnippets)
                {
                    ExecuteShowAllSnippets();
                }
                else
                {
                    ExecuteSelectFilter(item.FilterId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExecuteNavigateToItem: Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles navigation item selection change (called from View).
        /// </summary>
        partial void OnSelectedNavigationItemChanged(NavigationItem? oldValue, NavigationItem? newValue)
        {
            if (newValue != null)
            {
                ExecuteNavigateToItem(newValue);
            }
        }

        #endregion

        public void Refresh()
        {
            _ = RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            if (_codeService == null) return;
            try
            {
                var all = await _codeService.GetAllSnippetsAsync();
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
                System.Diagnostics.Debug.WriteLine($"CodeViewModel.RefreshAsync: Error refreshing snippets: {ex.Message}");
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
            if (_codeService == null) return;
            try
            {
                // Get all snippets and filter for deleted ones
                var all = await _codeService.GetAllSnippetsAsync();
                var deleted = all.Where(x => x.IsDeleted).ToList();
                _allSnippets = deleted;
                IsTrashMode = true;
                ApplyCodeTagFilters();
            }
            catch { }
        }

        public void FilterSnippets()
        {
            // Filter snippets using in-memory _allSnippets
            if (_activeFilters.Any())
            {
                var filtered = _allSnippets.Where(s =>
                {
                    var snippetTags = s.Tags?.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(t => t.Trim().ToLowerInvariant())
                                        .ToList() ?? new List<string>();
                    // Active filters are GUIDs - for new architecture, this filtering may need adjustment
                    // For now, just skip if no matching logic
                    return true;
                }).ToList();
                UpdateSnippetsOnUi(filtered);
            }
            else
            {
                UpdateSnippetsOnUi(_allSnippets);
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
                // Transient tracking removed - snippets are saved via CodeService
                System.Diagnostics.Debug.WriteLine("CodeViewModel.AddSnippet: created new snippet.");
            }
            catch { }
        }

        [RelayCommand]
        private async Task SaveSnippetAsync()
        {
            if (SelectedSnippet is null) return;
            var snippet = SelectedSnippet;
            
            // If snippet has no content, do not persist
            if (string.IsNullOrWhiteSpace(snippet.Content))
            {
                try
                {
                    Snippets.Remove(snippet);
                    SelectedSnippet = null;
                    IsDirty = false;
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CodeViewModel.SaveSnippetAsync: Error removing empty snippet: {ex.Message}");
                }
            }

            if (_codeService != null)
            {
                try
                {
                    await _codeService.SaveSnippetAsync(snippet);
                    IsDirty = false;
                    Refresh();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CodeViewModel.SaveSnippetAsync: Error saving snippet: {ex.Message}");
                }
            }
            else
            {
                IsDirty = false;
                System.Diagnostics.Debug.WriteLine("CodeViewModel.SaveSnippetAsync: CodeService is null; Save not performed.");
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
            try
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[DEBUG] SaveSnippetFileAsync: invoked for id={file.Id}");
#endif
                // If this snippet has no content, skip save
                if (string.IsNullOrWhiteSpace(file.Content))
                {
                    try
                    {
                        App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                        {
                            try { Snippets.Remove(file); } catch { }
                        });
                    }
                    catch { }
                    return;
                }

                // Use CodeService for save
                if (_codeService != null)
                {
                    await _codeService.SaveSnippetAsync(file);
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] SaveSnippetFileAsync: saved via CodeService id={file.Id}");
#endif
                    if (refreshAfterSave)
                    {
                        try { App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => Refresh()); } catch { }
                    }
                }
                else
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] SaveSnippetFileAsync: CodeService null, fallback export id={file.Id}");
#endif
                    // Fallback: export to JSON using default path (SettingsService removed)
                    try
                    {
                        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        var exportDir = System.IO.Path.Combine(docs, "Pivot", "CodeSnippets");
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
                        if (refreshAfterSave)
                        {
                            try { App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => Refresh()); } catch { }
                        }
                    }
                    catch { }
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
            if (_codeService != null)
            {
                _ = _codeService.DeleteSnippetAsync(snippet);
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
        private async Task Search()
        {
            if (_codeService != null)
            {
                var results = await _codeService.SearchAsync(SearchQuery);
                UpdateSnippetsOnUi(results);
            }
            else
            {
                var q = (SearchQuery ?? string.Empty).ToLowerInvariant();
                var results = _snippets.Where(s => (s.Title ?? string.Empty).ToLowerInvariant().Contains(q) || (s.Content ?? string.Empty).ToLowerInvariant().Contains(q) || (s.Tags ?? string.Empty).ToLowerInvariant().Contains(q)).ToList();
                UpdateSnippetsOnUi(results);
            }
        }

        // Helper: Returns all unique tags from loaded snippets
        public IEnumerable<Pivot.CodeModule.Models.CodeTag> GetAllTags()
        {
            // Get tags from _allSnippets instead of legacy _repo
            var tagNames = _allSnippets
                .Where(s => !string.IsNullOrEmpty(s.Tags))
                .SelectMany(s => s.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);
            
            return tagNames.Select(name => new Pivot.CodeModule.Models.CodeTag { Name = name });
        }

        public IEnumerable<CodeFile> GetSnippetsByTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return Enumerable.Empty<CodeFile>();
            var matches = _allSnippets.Where(s =>
            {
                var tags = (s.Tags ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim());
                return tags.Any(t => string.Equals(t, tagName, StringComparison.OrdinalIgnoreCase));
            });
            return matches;
        }

        // ApplyCachedValuesAsync removed - no longer using SnippetCacheService
        // Content is now synchronized via CodeService

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

        public async Task RemoveTagFromAllSnippetsAsync(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return;
            if (_codeService == null) return;
            
            // Get all snippets that might have this tag
            var all = await _codeService.GetAllSnippetsAsync();
            var toUpdate = new List<CodeFile>();
            
            foreach (var snippet in all)
            {
                if (string.IsNullOrEmpty(snippet.Tags)) continue;
                
                var tags = snippet.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => !string.Equals(t, tagName, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                
                var newTags = string.Join(", ", tags);
                if (!string.Equals(snippet.Tags, newTags))
                {
                    snippet.Tags = newTags;
                    toUpdate.Add(snippet);
                }
            }
            
            foreach (var snippet in toUpdate)
            {
                await _codeService.SaveSnippetAsync(snippet);
            }
            
            Refresh();
        }

        public async Task UpdateTagInAllSnippetsAsync(string oldTag, string newTag)
        {
            if (string.IsNullOrWhiteSpace(oldTag) || string.IsNullOrWhiteSpace(newTag)) return;
            if (_codeService == null) return;

            var all = await _codeService.GetAllSnippetsAsync();
            var toUpdate = new List<CodeFile>();

            foreach (var snippet in all)
            {
                if (string.IsNullOrEmpty(snippet.Tags)) continue;

                var tags = snippet.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .ToList();

                bool changed = false;
                var newTagList = new List<string>();
                
                foreach (var t in tags)
                {
                    if (string.Equals(t, oldTag, StringComparison.OrdinalIgnoreCase))
                    {
                        newTagList.Add(newTag);
                        changed = true;
                    }
                    else
                    {
                        newTagList.Add(t);
                    }
                }

                if (changed)
                {
                    snippet.Tags = string.Join(", ", newTagList.Distinct(StringComparer.OrdinalIgnoreCase));
                    toUpdate.Add(snippet);
                }
            }

            foreach (var snippet in toUpdate)
            {
                await _codeService.SaveSnippetAsync(snippet);
            }

            Refresh();
        }

        /// <summary>
        /// Copies snippet content to clipboard. Should be called from UI thread.
        /// </summary>
        [RelayCommand]
        private void CopyToClipboard(CodeFile? snippet)
        {
            if (snippet == null) return;
            try
            {
                var dp = new DataPackage();
                dp.SetText(snippet.Content ?? string.Empty);
                Clipboard.SetContent(dp);
            }
            catch { }
        }

        /// <summary>
        /// Restores a deleted snippet from trash.
        /// </summary>
        [RelayCommand]
        private async Task RestoreSnippetAsync(CodeFile? snippet)
        {
            if (snippet == null || _codeService == null) return;
            snippet.IsDeleted = false;
            snippet.DeletedAt = null;
            await _codeService.SaveSnippetAsync(snippet);
            Refresh();
        }

        /// <summary>
        /// Opens the containing directory for a snippet's source path.
        /// </summary>
        [RelayCommand]
        private void OpenContainingDirectory(CodeFile? snippet)
        {
            if (snippet == null) return;
            try
            {
                var path = snippet.FilePath;
                if (string.IsNullOrWhiteSpace(path)) return;

                // Check if path is a file or directory
                if (System.IO.File.Exists(path))
                {
                    // Select the file in explorer
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                else if (System.IO.Directory.Exists(path))
                {
                    // Open the directory
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
                }
                else
                {
                    // Try parent directory
                    var dir = System.IO.Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"\"{dir}\"");
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Creates a new snippet from text content (used by quick-add).
        /// Returns the created snippet for UI handling (scroll into view).
        /// </summary>
        public async Task<CodeFile?> CreateSnippetFromTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (_codeService == null) return null;

            // Derive title from first non-empty line
            var lines = text.Replace("\r", "\n").Split(new[] { '\n' }, StringSplitOptions.None);
            var title = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "Snippet";
            title = title.Length > 120 ? title.Substring(0, 120) : title;

            var newSnippet = new CodeFile
            {
                Title = title,
                Content = text,
                Updated = DateTime.Now
            };

            await _codeService.SaveSnippetAsync(newSnippet);

            // Add to UI collection
            if (Snippets != null)
            {
                var insertIndex = Snippets.Count > 0 && Snippets[0].Id == Guid.Empty ? 1 : 0;
                Snippets.Insert(insertIndex, newSnippet);
            }
            else
            {
                Refresh();
            }

            return newSnippet;
        }
    }
}


