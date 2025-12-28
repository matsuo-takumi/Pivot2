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

namespace Pivot.ViewModels
{
    public partial class ImageViewModel : ObservableObject, IRecipient<DirectoryChangedMessage>
    {
        private readonly LayoutService _layoutService = new();
        private readonly SortService _sortService = new();

        public ObservableCollection<TemplateItem> Images { get; set; } = new();


        // Navigation
        public ObservableCollection<FolderNode> FolderTree { get; } = new();
        private List<TemplateItem> _allImages = new();

        [ObservableProperty]
        private FolderNode? _selectedFolder;

        partial void OnSelectedFolderChanged(FolderNode? value)
        {
            FilterImages();
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

        public SelectionManagerViewModel<TemplateItem> SelectionManager { get; }

        private System.Threading.CancellationTokenSource? _loadCts;
        private SettingsService? _settings;
        private DirectorySettingsService? _directorySettings;
        private readonly IMessenger? _messenger;

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
                _settings = services.GetService<SettingsService>();
                _directorySettings = services.GetService<DirectorySettingsService>();
                _messenger = services.GetService<IMessenger>();

                if (_messenger != null)
                {
                    _messenger.RegisterAll(this);
                }

                if (_settings != null)
                {
                    SelectionBorderThickness = _settings.GetImageSelectionBorderThickness();
                    
                    // Load persisted layout and sort settings
                    CurrentLayout = _settings.GetImageLayoutMode();
                    CurrentSortField = _settings.GetImageSortField();
                    CurrentSortDirection = _settings.GetImageSortDirection();
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
            _ = _settings?.SetImageSortFieldAsync(CurrentSortField);
            _ = _settings?.SetImageSortDirectionAsync(CurrentSortDirection);
        }

        [RelayCommand]
        private void ToggleSortDirection()
        {
            CurrentSortDirection = CurrentSortDirection == SortDirection.Ascending 
                ? SortDirection.Descending 
                : SortDirection.Ascending;
            ApplySort();
            
            // Persist settings
            _ = _settings?.SetImageSortDirectionAsync(CurrentSortDirection);
        }

        private void ApplySort()
        {
            SortIcon = _sortService.GetSortIcon(CurrentSortField);
            SortDirectionIcon = _sortService.GetDirectionIcon(CurrentSortDirection);

            SortDirectionIcon = _sortService.GetDirectionIcon(CurrentSortDirection);

            // Sort current displayed images
            var sorted = _sortService.Sort(Images, CurrentSortField, CurrentSortDirection).ToList();
            Images.Clear();
            foreach (var item in sorted)
            {
                Images.Add(item);
            }
            

        }

        private void FilterImages()
        {
            // Filter based on selected folder
            IEnumerable<TemplateItem> filtered;
            
            if (SelectedFolder == null)
            {
                filtered = _allImages;
            }
            else
            {
                // Simple starts-with check for directory path
                var folderPath = SelectedFolder.FullPath;
                filtered = _allImages.Where(i => 
                    i.Path.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase));
            }

            // Apply Sort
            var sorted = _sortService.Sort(filtered, CurrentSortField, CurrentSortDirection).ToList();

            Images.Clear();
            foreach (var item in sorted)
            {
                Images.Add(item);
            }


        }

        partial void OnCurrentLayoutChanged(LayoutType value)
        {
            LayoutIcon = _layoutService.GetLayoutIcon(value);
            UseTextListMode = value == LayoutType.List;
            ShowThumbnails = !UseTextListMode;
            
            // Persist settings
            _ = _settings?.SetImageLayoutModeAsync(value);
        }

        public async void Receive(DirectoryChangedMessage message)
        {
            if (message.Value.Category == DirectoryCategory.Image && _directorySettings != null)
            {
                // Reload on directory change
                 await LoadFromDirectoriesAsync(_directorySettings.ImageDirectories);
            }
        }

        public async Task LoadFromDirectoriesAsync(IEnumerable<string> directories, int maxFiles = 100000)
        {
            if (directories == null) return;
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase){ ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga", ".tif", ".tiff", ".webp" };
            var files = new List<string>();
            
            // Fast file scan on background thread
            await Task.Run(() =>
            {
                foreach (var d in directories)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(d) || !Directory.Exists(d)) continue;
                        foreach (var f in Directory.EnumerateFiles(d, "*.*", SearchOption.AllDirectories))
                        {
                            if (exts.Contains(Path.GetExtension(f)))
                            {
                                files.Add(f);
                                if (files.Count >= maxFiles) return;
                            }
                        }
                    }
                    catch { }
                }
            });

            if (files.Count == 0) return;

            try { _loadCts?.Cancel(); } catch { }
            _loadCts = new System.Threading.CancellationTokenSource();
            var ct = _loadCts.Token;

            Images.Clear();
            _allImages.Clear(); // Clear cache
            var thumbService = App.Current.Services.GetService<Pivot.Services.IThumbnailService>();
            try
            {
                if (thumbService != null)
                {
                    var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var cacheDir = System.IO.Path.Combine(local, "Pivot", "cache", "thumbnails");
                    await thumbService.InitializeAsync(cacheDir, 500L * 1024 * 1024);
                }
            }
            catch { }

            // Parallel loading in chunks
            int chunkSize = 50;
            var chunks = files.Chunk(chunkSize);
            
            foreach (var chunk in chunks)
            {
                if (ct.IsCancellationRequested) break;
                
                var tasks = chunk.Select(f => CreateTemplateItemAsync(f, thumbService));
                var items = await Task.WhenAll(tasks);
                
                _allImages.AddRange(items);
            }

            // Build Directory Tree from root directories
            BuildDirectoryTree(directories);

            // Initial Filter (Show All)
            FilterImages();
        }

        private async Task<TemplateItem> CreateTemplateItemAsync(string f, IThumbnailService? thumbService)
        {
            long fileSize = 0;
            DateTime lastMod = DateTime.MinValue;
            try
            {
                var fi = new FileInfo(f);
                fileSize = fi.Length;
                lastMod = fi.LastWriteTime;
            }
            catch { }
            
            var item = new TemplateItem
            {
                Kind = AssetKind.Image,
                Path = f,
                Name = Path.GetFileName(f),
                Size = fileSize,
                LastModified = lastMod
            };

            // Calculate Aspect Ratio (async)
            try
            {
                // Note: Windows.Storage API usage in WinUI 3 Desktop
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(f);
                var props = await file.Properties.GetImagePropertiesAsync();
                
                item.PixelWidth = (int)props.Width;
                item.PixelHeight = (int)props.Height;
                if (item.PixelHeight > 0)
                {
                    item.AspectRatio = (double)item.PixelWidth / item.PixelHeight;
                }
            }
            catch 
            {
                // Fallback or ignore
            }

            try
            {
                if (thumbService != null)
                {
                    var cached = thumbService.TryGetCachedThumbnailPath(f, 300, 200);
                    if (!string.IsNullOrWhiteSpace(cached))
                    {
                        item.ThumbnailPath = new System.Uri(cached).AbsoluteUri;
                    }
                }
            }
            catch { }
            return item;
        }

        private void BuildDirectoryTree(IEnumerable<string> rootDirectories)
        {
            FolderTree.Clear();
            // Add "All" node or just root folders? User request implies directory tree.
            // Let's add root folders directly.
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
