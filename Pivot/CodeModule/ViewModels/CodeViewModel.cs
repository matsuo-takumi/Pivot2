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
        private readonly CodeTagService? _tagService;
        private readonly CodeNavigationService? _navigationService;
        // REMOVED: private List<CodeFile> _allSnippets - now using Snippets as single source of truth
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
        /// <summary>
        /// Main constructor using CodeService (new architecture).
        /// </summary>
        public CodeViewModel(
            CodeService codeService, 
            FilterSettingsService filterSettings,
            CodeTagService tagService,
            CodeNavigationService navigationService)
        {
            _codeService = codeService;
            _filterSettings = filterSettings;
            _tagService = tagService;
            _navigationService = navigationService;
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
            _tagService = null;
            _navigationService = null;
            _snippets = new ObservableCollection<CodeFile>();
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

        private async void ExecuteEmptyTrash()
        {
            if (_codeService == null) return;
            
            // Get deleted snippets from current UI list (already filtered by IsTrashMode)
            var trash = Snippets?.Where(x => x.IsDeleted).ToList() ?? new List<CodeFile>();
            if (!trash.Any()) return;

            foreach (var snippet in trash)
            {
                try 
                {
                    await _codeService.HardDeleteSnippetAsync(snippet);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deleting snippet {snippet.Id}: {ex.Message}");
                }
            }

            // Reload from DB to update UI
            await LoadFromDbAsync();
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
            if (_tagService != null)
            {
                return _tagService.CollectAvailableTags(Snippets);
            }

            // Fallback (should ideally not be hit if DI works)
            return new List<string>();
        }

        // CollectPreferenceTagNames removed - replaced by CodeTagService logic logic


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
            var allowedTags = _tagService?.GetAllowedPreferenceTags() ?? new HashSet<string>();
            
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
            // _tagService.ValidateAndCleanTags modifies the snippet directly
            // We want to return listing here, but ValidateAndCleanTags also saves?
            // Existing logic: "FilterSnippetTagsByPreferences" returns filtered list AND updates snippet if needed.
            
            if (snippet == null || _tagService == null) return new List<string>();
            
            // Actually, simply using ValidateAndCleanTags handles the update logic
            // But this method returns the list.
            
            var tags = ParseTagList(snippet.Tags);
            var allowed = _tagService.GetAllowedPreferenceTags();
            if (allowed.Count == 0) return tags;

            // Use service logic to validate and clean if needed
            if (_tagService.ValidateAndCleanTags(snippet))
            {
                _ = PersistSnippetAsync(snippet, refreshAfterSave: false);
                // Re-parse updated tags
                tags = ParseTagList(snippet.Tags);
            }
            
            return tags; 
        }

        /// <summary>
        /// Removes tags that don't exist in preferences from all snippets.
        /// This ensures that deleted tags are automatically removed from cards.
        /// </summary>
        private void RemoveInvalidTagsFromSnippets(List<CodeFile> snippets)
        {
            if (snippets == null || !snippets.Any() || _tagService == null) return;
            
            // Optimization: check if any preferences exist first (inside service)
            // But ValidateAndCleanTags checks GetAllowedPreferenceTags internally.
            
            bool updated = false;
            foreach (var snippet in snippets)
            {
                if (_tagService.ValidateAndCleanTags(snippet))
                {
                    _ = PersistSnippetAsync(snippet, refreshAfterSave: false);
                    updated = true;
                }
            }

            if (updated)
            {
                System.Diagnostics.Debug.WriteLine("RemoveInvalidTagsFromSnippets: Removed invalid tags from snippets");
            }
        }

        // GetAllowedPreferenceTagNames removed - use _tagService.GetAllowedPreferenceTags()

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
            if (snippet == null || string.IsNullOrWhiteSpace(tag.Name) || _tagService == null) return;
            
            bool changed = tag.IsSelected 
                ? _tagService.AddTagToSnippet(snippet, tag.Name)
                : _tagService.RemoveTagFromSnippet(snippet, tag.Name);

            if (changed)
            {
                await PersistSnippetAsync(snippet, refreshAfterSave: false);
            }
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
            return _tagService?.ParseTagList(raw) ?? new List<string>();
        }

        private string NormalizeTagName(string? input)
        {
            return _tagService?.NormalizeTagName(input) ?? (input ?? string.Empty).Trim();
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
                if (_navigationService != null)
                {
                    var items = _navigationService.GetNavigationItems();
                    foreach (var item in items)
                    {
                        NavigationItems.Add(item);
                    }
                }
                
                // Select "All Snippets" by default if available
                if (NavigationItems.Count > 0 && SelectedNavigationItem == null)
                {
                    SelectedNavigationItem = NavigationItems[0];
                }
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
            _ = LoadFromDbAsync();
        }

        /// <summary>
        /// Loads snippets from database. DB is the single source of truth.
        /// This replaces the old RefreshAsync/ApplyCodeTagFilters pattern.
        /// </summary>
        public async Task LoadFromDbAsync()
        {
            if (_codeService == null) return;
            try
            {
                System.Diagnostics.Debug.WriteLine("[CodeViewModel] LoadFromDbAsync: Loading from DB...");
                
                // Get all non-deleted snippets from DB
                var all = await _codeService.GetAllSnippetsAsync();
                
                // Filter out deleted snippets for normal view
                var visible = IsTrashMode 
                    ? all.Where(s => s.IsDeleted).ToList()
                    : all.Where(s => !s.IsDeleted).ToList();
                
                // Apply tag filters if any
                if (_selectedCodeTags.Any())
                {
                    visible = visible.Where(s =>
                    {
                        var snippetTags = (s.Tags ?? string.Empty)
                            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => t.Trim())
                            .ToList();
                        return _selectedCodeTags.Any(sel => 
                            snippetTags.Any(st => string.Equals(st, sel, StringComparison.OrdinalIgnoreCase)));
                    }).ToList();
                }
                
                // Update UI on dispatcher thread
                var dispatcher = App.Current.MainWindow?.DispatcherQueue;
                if (dispatcher != null)
                {
                    dispatcher.TryEnqueue(() =>
                    {
                        try
                        {
                            // Simply replace the collection - clean and simple
                            Snippets = new ObservableCollection<CodeFile>(visible);
                            System.Diagnostics.Debug.WriteLine($"[CodeViewModel] LoadFromDbAsync: Loaded {visible.Count} snippets");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CodeViewModel] LoadFromDbAsync: UI update error: {ex.Message}");
                        }
                    });
                }
                else
                {
                    Snippets = new ObservableCollection<CodeFile>(visible);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CodeViewModel] LoadFromDbAsync: Error: {ex.Message}");
            }
        }

        // Alias for backward compatibility
        public Task RefreshAsync() => LoadFromDbAsync();

        // Show snippets that are in Trash (soft-deleted) and still within the restore window.
        public void ShowDeletedSnippets()
        {
            IsTrashMode = true;
            _ = LoadFromDbAsync();
        }

        public void FilterSnippets()
        {
            // Filtering is now handled by LoadFromDbAsync with _selectedCodeTags
            _ = LoadFromDbAsync();
        }

        private void OnTagSelection(TagSelectionMessage msg)
        {
            try
            {
                var (tabId, tagName, isSelected) = msg.Value;
                if (!string.Equals(tabId, "Code", StringComparison.OrdinalIgnoreCase)) return;
                if (isSelected) _selectedCodeTags.Add(tagName);
                else _selectedCodeTags.Remove(tagName);
                // Reload from DB with new filters
                _ = LoadFromDbAsync();
            }
            catch { }
        }

        private void ApplyCodeTagFilters()
        {
            // Filtering is now handled by LoadFromDbAsync with _selectedCodeTags
            _ = LoadFromDbAsync();
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
            
            // Do NOT automatically delete if content is empty - allow empty snippets
            // CodeViewModel.SaveSnippetAsync: Logic removed

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
                // Do NOT automatically delete if content is empty
                // CodeViewModel.SaveSnippetFileAsync: Logic removed

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
                        // Update in Snippets collection (single source of truth)
                        var existing = Snippets?.FirstOrDefault(s => s.Id == file.Id);
                        if (existing != null)
                        {
                            CopySnippetValues(file, existing);
                            
                            // Update SelectedSnippet if it's the same file
                            if (SelectedSnippet != null && SelectedSnippet.Id == file.Id && !ReferenceEquals(SelectedSnippet, existing))
                            {
                                CopySnippetValues(file, SelectedSnippet);
                            }
                        }
                        else
                        {
                            // Insert new snippet
                            if (Snippets == null) Snippets = new ObservableCollection<CodeFile>();
                            Snippets.Insert(0, file);
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

        /// <summary>
        /// Deletes a specific snippet by reference (used by card delete buttons).
        /// </summary>
        [RelayCommand]
        private async Task DeleteSnippetByIdAsync(CodeFile? snippet)
        {
            if (snippet == null || snippet.Id == Guid.Empty) return;
            
            try
            {
                if (_codeService != null)
                {
                    await _codeService.DeleteSnippetAsync(snippet);
                }
                
                try { Snippets?.Remove(snippet); } catch { }
                
                if (SelectedSnippet == snippet)
                {
                    SelectedSnippet = null;
                }
                
                Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteSnippetByIdAsync: Error: {ex.Message}");
            }
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
            // Get tags using service
            var tags = _tagService?.CollectAvailableTags(Snippets) ?? Enumerable.Empty<string>();
            return tags.Select(name => new Pivot.CodeModule.Models.CodeTag { Name = name });
        }

        public IEnumerable<CodeFile> GetSnippetsByTag(string tagName)
        {
            return _tagService?.FindSnippetsByTag(Snippets, tagName) ?? Enumerable.Empty<CodeFile>();
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

            // Save to DB (this is the single source of truth)
            await _codeService.SaveSnippetAsync(newSnippet);
            System.Diagnostics.Debug.WriteLine($"[CodeViewModel] CreateSnippetFromTextAsync: Saved snippet '{title}' to DB");

            // Update UI - insert at beginning of list
            var dispatcher = App.Current.MainWindow?.DispatcherQueue;
            if (dispatcher != null)
            {
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        if (Snippets == null)
                        {
                            Snippets = new ObservableCollection<CodeFile>();
                        }
                        Snippets.Insert(0, newSnippet);
                        System.Diagnostics.Debug.WriteLine($"[CodeViewModel] CreateSnippetFromTextAsync: Added to UI, total count: {Snippets.Count}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CodeViewModel] CreateSnippetFromTextAsync: UI update error: {ex.Message}");
                    }
                });
            }
            else
            {
                // Direct update (if on UI thread already)
                Snippets ??= new ObservableCollection<CodeFile>();
                Snippets.Insert(0, newSnippet);
            }

            return newSnippet;
        }
    }
}


