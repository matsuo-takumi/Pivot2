using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pivot.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Services;

using CommunityToolkit.Mvvm.Messaging;
using Pivot.Messages;
using Microsoft.UI.Dispatching;

namespace Pivot.ViewModels
{
    public partial class ImageViewModel : ObservableObject, 
        IRecipient<DirectoryChangedMessage>,
        IRecipient<AssetChangedMessage>,
        IRecipient<BulkItemsChangedMessage<AssetEntity>>
    {
        private readonly LayoutService _layoutService = new();
        private readonly SortService _sortService = new();
        private readonly DispatcherQueue _dispatcherQueue;
        
        private static readonly HashSet<string> _imageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga", ".tif", ".tiff", ".webp"
        };

        [ObservableProperty]
        private Utils.RangeObservableCollection<TemplateItem> _images = new();

        // Navigation
        public ObservableCollection<FolderNode> FolderTree { get; } = new();
        private List<TemplateItem> _allImages = new();

        [ObservableProperty]
        private FolderNode? _selectedFolder;

        partial void OnSelectedFolderChanged(FolderNode? value)
        {
            _dispatcherQueue.TryEnqueue(() => FilterImages());
        }

        [ObservableProperty]
        private LayoutType _currentLayout = LayoutType.Grid;

        [ObservableProperty]
        private string _layoutIcon = "\uF0E2";

        [ObservableProperty]
        private bool _useTextListMode = false;

        [ObservableProperty]
        private bool _showThumbnails = true;

        [ObservableProperty]
        private double _selectionBorderThickness = 2.0;

        // Sort properties
        [ObservableProperty]
        private SortField _currentSortField = SortField.Name;

        [ObservableProperty]
        private SortDirection _currentSortDirection = SortDirection.Ascending;

        [ObservableProperty]
        private string _sortIcon = "\uE8AC";

        [ObservableProperty]
        private string _sortDirectionIcon = "\uE74A";

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private int _totalAssetCount = 0;

        public SelectionManagerViewModel<TemplateItem> SelectionManager { get; }

        private System.Threading.CancellationTokenSource? _loadCts;
        private ThemeSettingsService? _themeSettings;
        private ImageDisplaySettingsService? _imageDisplaySettings;
        private DirectorySettingsService? _directorySettings;
        private readonly IMessenger? _messenger;
        private readonly MetadataService? _metadataService;
        private readonly IThumbnailService? _thumbnailService;
        private IEnumerable<string>? _currentDirectories;
        private bool _thumbnailServiceInitialized = false;

        public ImageViewModel()
        {
            SelectionManager = new SelectionManagerViewModel<TemplateItem>();
            SelectionManager.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectionManagerViewModel<TemplateItem>.SelectedCount))
                {
                    if (Images != null)
                    {
                        foreach (var item in Images)
                        {
                            item.IsSelected = SelectionManager.IsSelected(item);
                        }
                    }
                }
            };

            try
            {
                var services = App.Current.Services;
                _themeSettings = services.GetService<ThemeSettingsService>();
                _imageDisplaySettings = services.GetService<ImageDisplaySettingsService>();
                _directorySettings = services.GetService<DirectorySettingsService>();
                _messenger = services.GetService<IMessenger>();
                _metadataService = services.GetService<MetadataService>();
                _thumbnailService = services.GetService<IThumbnailService>();

                _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            
                if (_messenger != null)
                {
                    _messenger.RegisterAll(this);
                }

                if (_themeSettings != null)
                {
                    SelectionBorderThickness = _themeSettings.ImageSelectionBorderThickness;
                }
                
                if (_imageDisplaySettings != null)
                {
                    // Load persisted layout and sort settings
                    CurrentLayout = _imageDisplaySettings.GetLayoutMode();
                    CurrentSortField = _imageDisplaySettings.GetSortField();
                    CurrentSortDirection = _imageDisplaySettings.GetSortDirection();
                }
            }
            catch { }

            LayoutIcon = _layoutService.GetLayoutIcon(CurrentLayout);
            SortIcon = _sortService.GetSortIcon(CurrentSortField);
            SortDirectionIcon = _sortService.GetDirectionIcon(CurrentSortDirection);
            
            UseTextListMode = _currentLayout == LayoutType.List;
            ShowThumbnails = !UseTextListMode;
        }

        [RelayCommand]
        private void ToggleLayout()
        {
            CurrentLayout = _layoutService.GetNextLayout(CurrentLayout);
        }

        [RelayCommand]
        private void SetSortField(object parameter)
        {
            SortField field;
            if (parameter is SortField sf)
            {
                field = sf;
            }
            else if (parameter is string s && Enum.TryParse<SortField>(s, out var parsed))
            {
                field = parsed;
            }
            else
            {
                return;
            }

            if (CurrentSortField == field)
            {
                CurrentSortDirection = CurrentSortDirection == SortDirection.Ascending 
                    ? SortDirection.Descending 
                    : SortDirection.Ascending;
            }
            else
            {
                CurrentSortField = field;
                CurrentSortDirection = SortDirection.Ascending;
            }
            ApplySort();
            
            // Persist settings
            _ = _imageDisplaySettings?.SetSortFieldAsync(CurrentSortField);
            _ = _imageDisplaySettings?.SetSortDirectionAsync(CurrentSortDirection);
        }

        [RelayCommand]
        private void ToggleSortDirection()
        {
            CurrentSortDirection = CurrentSortDirection == SortDirection.Ascending 
                ? SortDirection.Descending 
                : SortDirection.Ascending;
            ApplySort();
            
            // Persist settings
            _ = _imageDisplaySettings?.SetSortDirectionAsync(CurrentSortDirection);
        }

        private void ApplySort()
        {
            SortIcon = _sortService.GetSortIcon(CurrentSortField);
            SortDirectionIcon = _sortService.GetDirectionIcon(CurrentSortDirection);

            // Sort _allImages in place
            var sorted = _sortService.Sort(_allImages, CurrentSortField, CurrentSortDirection).ToList();
            _allImages.Clear();
            _allImages.AddRange(sorted);
            
            // Refresh filter to apply new sort order
            FilterImages();
        }

        private void FilterImages()
        {
            IEnumerable<TemplateItem> filtered;
            
            if (SelectedFolder == null)
            {
                filtered = _allImages;
            }
            else
            {
                var folderPath = SelectedFolder.FullPath;
                filtered = _allImages.Where(i => 
                    i.Path.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase));
            }

            var newItems = _sortService.Sort(filtered, CurrentSortField, CurrentSortDirection).ToList();

            // Optimized update: only modify what changed
            SynchronizeCollection(Images, newItems);
        }
        
        /// <summary>
        /// Synchronize collection by only adding/removing changed items.
        /// This prevents unnecessary UI updates and flickering.
        /// </summary>
        private void SynchronizeCollection(Utils.RangeObservableCollection<TemplateItem> target, List<TemplateItem> source)
        {
            // Quick path: if both empty, do nothing
            if (source.Count == 0 && target.Count == 0) return;
            
            // Quick path: if target empty, add all
            if (target.Count == 0)
            {
                target.AddRange(source);
                return;
            }
            
            // Quick path: if source empty, clear all
            if (source.Count == 0)
            {
                target.Clear();
                return;
            }
            
            // Create lookup for fast comparison
            var targetSet = new HashSet<TemplateItem>(target);
            var sourceSet = new HashSet<TemplateItem>(source);
            
            // Remove items not in source
            var toRemove = target.Where(item => !sourceSet.Contains(item)).ToList();
            foreach (var item in toRemove)
            {
                target.Remove(item);
            }
            
            // Add items not in target
            var toAdd = source.Where(item => !targetSet.Contains(item)).ToList();
            if (toAdd.Any())
            {
                target.AddRange(toAdd);
            }
            
            // Reorder if needed (only if items are same but order differs)
            if (target.Count == source.Count && toRemove.Count == 0 && toAdd.Count == 0)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    if (!ReferenceEquals(target[i], source[i]))
                    {
                        // Order differs, need to re-sync
                        target.ReplaceRange(source);
                        break;
                    }
                }
            }
        }

        partial void OnCurrentLayoutChanged(LayoutType value)
        {
            LayoutIcon = _layoutService.GetLayoutIcon(value);
            UseTextListMode = value == LayoutType.List;
            ShowThumbnails = !UseTextListMode;
            
            _ = _imageDisplaySettings?.SetLayoutModeAsync(value);
        }

        public async void Receive(DirectoryChangedMessage message)
        {
            if (message.Value.Category == DirectoryCategory.Image && _directorySettings != null)
            {
                await LoadAsync(_directorySettings.ImageDirectories);
            }
        }

        public void Receive(AssetChangedMessage message)
        {
             // Handle single update
             if (message.Value.Type == AssetChangedMessageData.ChangeType.Added)
             {
                 /* Add logic */ 
                 // Simplified for safety: just relying on Bulk or Refresh for now usually, 
                 // but for responsiveness we should handle it.
             }
        }

        public void Receive(BulkItemsChangedMessage<AssetEntity> message)
        {
            // Skip reactive updates during initial load to prevent flickering
            if (IsLoading) return;
            
            _dispatcherQueue.TryEnqueue(() => 
            {
                // Handle bulk update from Scanner
                foreach (var change in message.Value)
                {
                    // Check extension
                    var path = change.Item?.FilePath;
                    if (path == null) continue;
                    var ext = Path.GetExtension(path);
                    if (string.IsNullOrEmpty(ext) || !_imageExtensions.Contains(ext)) continue;

                    if (change.Type == ItemChangeData<AssetEntity>.ChangeType.Added || change.Type == ItemChangeData<AssetEntity>.ChangeType.Updated)
                    {
                        var asset = change.Item;
                        if (asset == null) continue;
                        
                        var existing = _allImages.FirstOrDefault(i => i.Path == asset.FilePath);
                        if (existing == null)
                        {
                           var newItem = new TemplateItem { 
                               Kind = AssetKind.Image, Path = asset.FilePath, Name = asset.FileName, Size = asset.FileSize, LastModified = asset.UpdatedAt 
                           };
                           _allImages.Add(newItem);
                        }
                        else
                        {
                           existing.Size = asset.FileSize;
                           existing.LastModified = asset.UpdatedAt;
                           existing.PixelWidth = asset.Width ?? 0;
                           existing.PixelHeight = asset.Height ?? 0;
                           existing.AspectRatio = (asset.Height ?? 0) > 0 ? (double)(asset.Width ?? 0) / (asset.Height ?? 0) : 1.0;
                        }
                    }
                    else if (change.Type == ItemChangeData<AssetEntity>.ChangeType.Deleted)
                    {
                        var existing = _allImages.FirstOrDefault(i => i.Path == change.Item?.FilePath);
                        if (existing != null) _allImages.Remove(existing);
                    }
                }
                FilterImages(); // Refresh UI
            });
        }

        // Legacy: AssetFileChangedMessage receiver (commented out - use BulkItemsChangedMessage instead)
        /*
        public void Receive(AssetFileChangedMessage message)
        {
            _dispatcherQueue.TryEnqueue(() => 
            {
                try
                {
                    switch (message.Type)
                    {
                        case AssetFileChangedMessage.ChangeType.Added:
                            if (message.Asset != null)
                            {
                                // var item = AssetToTemplateItem(message.Asset);
                                // _allImages.Add(item);
                                // FilterImages();
                            }
                            break;
                        case AssetFileChangedMessage.ChangeType.Updated:
                            if (message.Asset != null)
                            {
                                var existing = _allImages.FirstOrDefault(i => 
                                    i.Path.Equals(message.FilePath, StringComparison.OrdinalIgnoreCase));
                                if (existing != null)
                                {
                                    // UpdateTemplateItemFromAsset(existing, message.Asset);
                                }
                            }
                            break;
                        case AssetFileChangedMessage.ChangeType.Deleted:
                            var toRemove = _allImages.FirstOrDefault(i => 
                                i.Path.Equals(message.FilePath, StringComparison.OrdinalIgnoreCase));
                            if (toRemove != null)
                            {
                                _allImages.Remove(toRemove);
                                Images.Remove(toRemove);
                            }
                            break;
                    }
                }
                catch { }
            });
        }
        */

        /// <summary>
        /// Main load method. DB-first, fallback to filesystem.
        /// </summary>
        public async Task LoadAsync(IEnumerable<string> directories)
        {
            if (directories == null) return;
            _currentDirectories = directories.ToList();

            // Phase 3: Load from DB immediately
            await LoadFromDatabaseAsync();
            
            // Note: Background scanning is handled by MainViewModel -> FileScannerService.
            // Updates will arrive via Messenger (AssetChangedMessage / BulkItemsChangedMessage).
        }

        public async Task LoadFromDatabaseAsync()
        {
            if (_metadataService == null) return;
            _dispatcherQueue.TryEnqueue(() => IsLoading = true);
            try
            {
                // Initialize thumbnail service if needed
                if (_thumbnailService != null && !_thumbnailServiceInitialized)
                {
                    var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var cacheDir = Path.Combine(local, "Pivot", "cache", "thumbnails");
                    await _thumbnailService.InitializeAsync(cacheDir, 500L * 1024 * 1024);
                    _thumbnailServiceInitialized = true;
                }
                
                // Collect items in batches to avoid flickering
                var allItems = new List<TemplateItem>();
                var missingDimensions = new List<TemplateItem>();
                
                await foreach (var asset in _metadataService.StreamAssetsAsync(200))
                {
                   var ext = Path.GetExtension(asset.FilePath);
                   if (string.IsNullOrEmpty(ext) || !_imageExtensions.Contains(ext)) continue;

                   // Resolve thumbnail path: use cached if exists, otherwise check cache
                   string? thumbnailPath = null;
                   if (!string.IsNullOrEmpty(asset.ThumbnailPath) && File.Exists(asset.ThumbnailPath))
                   {
                       thumbnailPath = new Uri(asset.ThumbnailPath).AbsoluteUri;
                   }
                   else if (_thumbnailService != null)
                   {
                       var cached = _thumbnailService.TryGetCachedThumbnailPath(asset.FilePath, 300, 200);
                       if (!string.IsNullOrEmpty(cached))
                       {
                           thumbnailPath = new Uri(cached).AbsoluteUri;
                       }
                   }

                   var item = new TemplateItem 
                   {
                       Kind = AssetKind.Image,
                       Path = asset.FilePath,
                       Name = asset.FileName,
                       Size = asset.FileSize,
                       LastModified = asset.UpdatedAt,
                       PixelWidth = asset.Width ?? 0,
                       PixelHeight = asset.Height ?? 0,
                       AspectRatio = (asset.Height ?? 0) > 0 ? (double)(asset.Width ?? 0) / (asset.Height ?? 0) : 1.0,
                       ThumbnailPath = thumbnailPath
                   };
                   
                   // Fallback: If dimensions missing, track for on-the-fly resolution
                   if (item.PixelWidth == 0 && File.Exists(item.Path))
                   {
                       missingDimensions.Add(item);
                   }
                   
                   allItems.Add(item);
                }
                
                // Start background resolution for missing dimensions (fire and forget)
                if (missingDimensions.Any())
                {
                    _ = ResolveMissingDimensionsAsync(missingDimensions);
                }
                
                // Single UI update - replace entire collection (1 notification instead of N)
                _dispatcherQueue.TryEnqueue(() => 
                {
                    _allImages.Clear();
                    _allImages.AddRange(allItems);
                    
                    // Sort and filter items
                    var sorted = _sortService.Sort(allItems, CurrentSortField, CurrentSortDirection)
                        .Where(item => _selectedFolder == null || item.Path.StartsWith(_selectedFolder.FullPath, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    
                    // Use ReplaceRange for flicker-free update
                    Images.ReplaceRange(sorted);
                    
                    // Update sort icons
                    SortIcon = _sortService.GetSortIcon(CurrentSortField);
                    SortDirectionIcon = _sortService.GetDirectionIcon(CurrentSortDirection);
                    
                    // Build directory tree for left column
                    if (_currentDirectories != null && _currentDirectories.Any())
                    {
                        BuildDirectoryTree(_currentDirectories);
                    }
                });
            }
            catch (Exception ex)
            {
               // Database load failed - log error instead of falling back to legacy
               System.Diagnostics.Debug.WriteLine($"LoadFromDatabaseAsync failed: {ex.Message}");
            }
            finally
            {
                _dispatcherQueue.TryEnqueue(() => IsLoading = false);
            }
        }
        
        private async Task ResolveMissingDimensionsAsync(List<TemplateItem> items)
        {
            await Task.Run(async () => 
            {
                foreach (var item in items)
                {
                    try 
                    {
                        // Use ImageSharp to identify dimensions quickly
                        var info = await SixLabors.ImageSharp.Image.IdentifyAsync(item.Path);
                        if (info != null && info.Width > 0 && info.Height > 0)
                        {
                            _dispatcherQueue.TryEnqueue(() => 
                            {
                                item.PixelWidth = info.Width;
                                item.PixelHeight = info.Height;
                                item.AspectRatio = (double)info.Width / info.Height;
                            });
                        }
                    }
                    catch { /* Ignore errors during background resolution */ }
                }
            });
        }

        // Legacy LoadFromDirectoriesAsync and CreateTemplateItemAsync removed - DB-first pattern only

        private void BuildDirectoryTree(IEnumerable<string> rootDirectories)
        {
            FolderTree.Clear();
            foreach (var dir in rootDirectories)
            {
                if (Directory.Exists(dir))
                {
                    var node = new FolderNode(new DirectoryInfo(dir).Name, dir);
                    BuildDirectoryTreeRecursive(node);
                    FolderTree.Add(node);
                }
            }
        }

        private void BuildDirectoryTreeRecursive(FolderNode node)
        {
            try
            {
                var subDirs = Directory.GetDirectories(node.FullPath);
                foreach (var dir in subDirs)
                {
                    var subNode = new FolderNode(new DirectoryInfo(dir).Name, dir);
                    BuildDirectoryTreeRecursive(subNode);
                    node.Children.Add(subNode);
                }
            }
            catch { }
        }

        public void CancelLoads()
        {
            try { _loadCts?.Cancel(); } catch { }
        }

        public void HandleSelection(TemplateItem item, bool isCtrl, bool isShift)
        {
            if (item == null) return;

            if (isShift && SelectionManager.LastSelectedItem != null)
            {
                var start = Images.IndexOf(SelectionManager.LastSelectedItem);
                var end = Images.IndexOf(item);

                if (start >= 0 && end >= 0)
                {
                    var range = new List<TemplateItem>();
                    int low = Math.Min(start, end);
                    int high = Math.Max(start, end);

                    for (int i = low; i <= high; i++)
                    {
                        if (i < Images.Count)
                        {
                            range.Add(Images[i]);
                        }
                    }

                    SelectionManager.SelectRange(range);
                    return;
                }
            }

            SelectionManager.SelectItem(item, isCtrl, isShift);
        }
    }
}
