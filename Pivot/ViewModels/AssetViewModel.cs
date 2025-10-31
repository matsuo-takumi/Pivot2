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
using Pivot.ViewModels;

namespace Pivot.ViewModels
{
    public partial class AssetViewModel : ObservableObject
    {
        public ObservableCollection<TemplateItem> Assets { get; set; }

        // FilterService-backed filters
        public ObservableCollection<FilterViewModel> Filters { get; } = new ObservableCollection<FilterViewModel>();

        [ObservableProperty]
        private FilterViewModel? _selectedFilter;

        private readonly FilterService? _filterService;

        // Assets after applying current filter
        public ObservableCollection<TemplateItem> DisplayedAssets { get; } = new ObservableCollection<TemplateItem>();

        public ObservableCollection<ObservableCollection<TemplateItem>> MasonryColumns { get; } = new ObservableCollection<ObservableCollection<TemplateItem>>();
// ... existing code ...

        private int _masonryColumnCount = 3;
        public int MasonryColumnCount
        {
            get => _masonryColumnCount;
            set
            {
                if (value <= 0) return;
                _masonryColumnCount = value;
                // rebuild using currently displayed (filtered) assets so resizing preserves filter
                BuildMasonryColumns(DisplayedAssets);
            }
        }

        private double _masonryColumnWidth = 200.0;
        public double MasonryColumnWidth
        {
            get => _masonryColumnWidth;
            set
            {
                if (value <= 0) return;
                _masonryColumnWidth = value;
                // rebuild using currently displayed (filtered) assets so resizing preserves filter
                BuildMasonryColumns(DisplayedAssets);
            }
        }

        [ObservableProperty]
        private LayoutType _currentLayout = LayoutType.Grid;

        [ObservableProperty]
        private bool _useTextListMode = false;

        [ObservableProperty]
        private bool _showThumbnails = true;

        [RelayCommand]
        private void ToggleLayout()
        {
            CurrentLayout = (LayoutType)(((int)CurrentLayout + 1) % 3);
        }

        private System.Threading.CancellationTokenSource? _loadCts;

        public AssetViewModel()
        {
            Assets = new ObservableCollection<TemplateItem>
            {
                new TemplateItem { Name = "Test Asset 1", Kind = AssetKind.Model, ThumbnailPath = "https://via.placeholder.com/160x120?text=Asset+1" },
                new TemplateItem { Name = "Test Asset 2", Kind = AssetKind.Model, ThumbnailPath = "https://via.placeholder.com/300x260?text=Asset+2" },
                new TemplateItem { Name = "Test Asset 3", Kind = AssetKind.Model, ThumbnailPath = "https://via.placeholder.com/200x180?text=Asset+3" },
                new TemplateItem { Name = "Test Asset 4", Kind = AssetKind.Model, ThumbnailPath = "https://via.placeholder.com/400x140?text=Asset+4" },
                new TemplateItem { Name = "Test Asset 5", Kind = AssetKind.Model, ThumbnailPath = "https://via.placeholder.com/120x200?text=Asset+5" }
            };

            BuildMasonryColumns();
            UseTextListMode = _currentLayout == LayoutType.List;
            ShowThumbnails = !UseTextListMode;

            // obtain FilterService from DI and wire up
            try
            {
                _filterService = App.Current.Services.GetService<FilterService>();
                if (_filterService != null)
                {
                    // mirror list reference
                    foreach (var f in _filterService.Filters) Filters.Add(f);
                    // subscribe to changes on service - reapply filters when selection changes
                    _filterService.PropertyChanged += (s, e) =>
                    {
                        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(FilterService.SelectedFilterIds))
                        {
                            ApplyFilterFromService();
                        }
                    };
                }
            }
            catch { }

            // initialize displayed assets
            ApplyFilterFromService();
        }

        partial void OnSelectedFilterChanged(FilterViewModel? value)
        {
            try
            {
                if (_filterService != null)
                {
                    if (value == null) _filterService.ClearSelectedFilters();
                    else _filterService.SetSelectedFilters(new[] { value.Id });
                    // Note: TagFilterControl persists per-tab selection; here we just update service selection
                }
            }
            catch { }
            ApplyFilterFromService();
        }

        private void ApplyFilterFromService()
        {
            DisplayedAssets.Clear();
            try
            {
                if (_filterService == null)
                {
                    foreach (var a in Assets) DisplayedAssets.Add(a);
                    return;
                }
                var res = _filterService.ApplyFilter(Assets);
                foreach (var a in res) DisplayedAssets.Add(a);
            }
            catch
            {
                foreach (var a in Assets) DisplayedAssets.Add(a);
            }

            // rebuild masonry columns from displayed assets so Masonry layout shows filtered items
            try
            {
                BuildMasonryColumns(DisplayedAssets);
            }
            catch { }
        }

        public async Task LoadFromDirectoriesAsync(IEnumerable<string> directories, int maxFiles = 200)
        {
            if (directories == null) return;
            // var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase){ ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga", ".tif", ".tiff", ".webp" };
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase){ ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tga", ".tif", ".tiff", ".mp4", ".mov", ".avi", ".mkv", ".webm", ".obj", ".fbx", ".gltf", ".glb", ".dae", ".mp3", ".wav", ".ogg", ".flac", ".aac" };
            var files = new List<string>();
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

            Assets.Clear();
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

            foreach (var f in files)
            {
                var ext = Path.GetExtension(f);
                AssetKind kind = AssetKind.Other;
                if (new HashSet<string>(StringComparer.OrdinalIgnoreCase){ ".obj",".fbx",".gltf",".glb",".dae" }.Contains(ext)) kind = AssetKind.Model;
                else if (new HashSet<string>(StringComparer.OrdinalIgnoreCase){ ".png",".jpg",".jpeg",".bmp",".gif",".webp",".tga",".tif",".tiff" }.Contains(ext)) kind = AssetKind.Image;
                else if (new HashSet<string>(StringComparer.OrdinalIgnoreCase){ ".mp4",".mov",".avi",".mkv",".webm" }.Contains(ext)) kind = AssetKind.Video;
                else if (new HashSet<string>(StringComparer.OrdinalIgnoreCase){ ".mp3",".wav",".ogg",".flac",".aac" }.Contains(ext)) kind = AssetKind.Audio;

                var item = new TemplateItem
                {
                    Kind = kind,
                    Path = f,
                    Name = Path.GetFileName(f)
                };
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
                Assets.Add(item);
            }

            // apply current filter to newly loaded assets
            try { ApplyFilterFromService(); } catch { }

            BuildMasonryColumns();

            try
            {
                var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                if (thumbService == null) return;

                var tasks = new List<Task>();
                foreach (var item in Assets.ToList())
                {
                    var originalPath = item.Path;
                    if (string.IsNullOrWhiteSpace(originalPath) || !File.Exists(originalPath)) continue;
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var thumbPath = await thumbService.GetOrCreateThumbnailAsync(originalPath, 300, 200, ct).ConfigureAwait(false);
                            var thumbUri = new System.Uri(thumbPath).AbsoluteUri;
                            if (dispatcher != null)
                            {
                                dispatcher.TryEnqueue(() => item.ThumbnailPath = thumbUri);
                            }
                            else
                            {
                                item.ThumbnailPath = thumbUri;
                            }
                        }
                        catch { }
                    }));
                }

                try { await Task.WhenAll(tasks).ConfigureAwait(false); } catch { }
            }
            catch { }
        }

        public void BuildMasonryColumns(IEnumerable<TemplateItem>? source = null)
        {
            var items = source ?? Assets;
            MasonryColumns.Clear();
            for (int i = 0; i < MasonryColumnCount; i++)
            {
                MasonryColumns.Add(new ObservableCollection<TemplateItem>());
            }

            var columnHeights = new int[MasonryColumnCount];
            foreach (var item in items)
            {
                int h = EstimateHeightFromUrl(item.ThumbnailPath);
                int minIndex = 0;
                for (int i = 1; i < MasonryColumnCount; i++)
                {
                    if (columnHeights[i] < columnHeights[minIndex]) minIndex = i;
                }
                MasonryColumns[minIndex].Add(item);
                columnHeights[minIndex] += h;
            }
        }

// ... existing code ...

// ... existing code ...

        public void CancelLoads()
        {
            try { _loadCts?.Cancel(); } catch { }
        }

        private static int EstimateHeightFromUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return 180;
            var m = Regex.Match(url, "(\\d+)x(\\d+)");
            if (m.Success && int.TryParse(m.Groups[2].Value, out int h))
            {
                return h;
            }
            return 180;
        }
    }
}
