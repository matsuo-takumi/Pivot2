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

namespace Pivot.ViewModels
{
    public partial class AssetViewModel : ObservableObject
    {
        public ObservableCollection<TemplateItem> Assets { get; set; }
        private List<TemplateItem> _allAssets = new List<TemplateItem>();
        private TagFilterViewModel? _tagFilterVm = App.Current.Services.GetService<TagFilterViewModel>();

        [ObservableProperty]
        private LayoutType _currentLayout = LayoutType.Grid;

        [ObservableProperty]
        private bool _useTextListMode = false;

        [ObservableProperty]
        private bool _showThumbnails = true;

        [RelayCommand]
        private void ToggleLayout()
        {
            CurrentLayout = (LayoutType)(((int)CurrentLayout + 1) % 4);
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

            UseTextListMode = _currentLayout == LayoutType.List;
            ShowThumbnails = !UseTextListMode;

            // Subscribe to tag filter changes to re-apply filter automatically
            try
            {
                if (_tagFilterVm != null)
                {
                    _tagFilterVm.SelectedTagsChanged += () => ApplyTagFilter();
                }
            }
            catch { }
        }

        partial void OnCurrentLayoutChanged(LayoutType value)
        {
            UseTextListMode = value == LayoutType.List;
            ShowThumbnails = !UseTextListMode;
        }

        public async Task LoadFromDirectoriesAsync(IEnumerable<string> directories, int maxFiles = 200)
        {
            if (directories == null) return;
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase){ ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga", ".tif", ".tiff", ".webp",
                // 3D model extensions
                ".obj", ".fbx", ".stl", ".glb", ".gltf", ".3ds", ".dae" };
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

            // maintain all assets list and apply filter instead of clearing directly
            _allAssets.Clear();
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
                var item = new TemplateItem
                {
                    Kind = AssetKind.Other,
                    Path = f,
                    Name = Path.GetFileName(f)
                };
                try
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    // determine kind
                    if (new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga", ".tif", ".tiff", ".webp" }.Contains(ext))
                    {
                        item.Kind = AssetKind.Image;
                    }
                    else if (new[] { ".obj", ".fbx", ".stl", ".glb", ".gltf", ".3ds", ".dae" }.Contains(ext))
                    {
                        item.Kind = AssetKind.Model;
                    }
                    if (thumbService != null)
                    {
                        var cached = thumbService.TryGetCachedThumbnailPath(f, 300, 200);
                        if (!string.IsNullOrWhiteSpace(cached))
                        {
                            // If it's a ms-appx URI, use directly; otherwise convert to file:// URI
                            if (cached.StartsWith("ms-appx://", StringComparison.OrdinalIgnoreCase))
                            {
                                item.ThumbnailPath = cached;
                            }
                            else
                            {
                                item.ThumbnailPath = new System.Uri(cached).AbsoluteUri;
                            }
                        }
                        else if (item.Kind == AssetKind.Model)
                        {
                            // fallback icon path for models
                            var extName = ext.TrimStart('.');
                            item.ThumbnailPath = $"ms-appx:///Assets/Icons/model_{extName}.png";
                        }
                    }
                }
                catch { }
                _allAssets.Add(item);
            }

            // After loading, apply tag filter which will update the public Assets collection
            ApplyTagFilter();

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

        public void CancelLoads()
        {
            try { _loadCts?.Cancel(); } catch { }
        }

        // Masonry layout support
        public ObservableCollection<ObservableCollection<TemplateItem>> MasonryColumns { get; } = new ObservableCollection<ObservableCollection<TemplateItem>>();

        private int _masonryColumnCount = 3;
        public int MasonryColumnCount
        {
            get => _masonryColumnCount;
            set
            {
                if (value <= 0) return;
                _masonryColumnCount = value;
                BuildMasonryColumns();
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
                BuildMasonryColumns();
            }
        }

        public void BuildMasonryColumns()
        {
            MasonryColumns.Clear();
            if (MasonryColumnCount <= 0) MasonryColumnCount = 1;
            for (int i = 0; i < MasonryColumnCount; i++) MasonryColumns.Add(new ObservableCollection<TemplateItem>());

            if (Assets == null) return;
            int idx = 0;
            foreach (var item in Assets)
            {
                MasonryColumns[idx % MasonryColumnCount].Add(item);
                idx++;
            }
        }

        // Justified layout support (minimal implementation)
        public ObservableCollection<ObservableCollection<JustifiedItem>> JustifiedRows { get; } = new ObservableCollection<ObservableCollection<JustifiedItem>>();
        public double JustifiedRowHeight { get; set; } = 140.0;

        public void BuildJustifiedRows(double containerWidth, double horizontalSpacing)
        {
            if (containerWidth <= 0) return;
            JustifiedRows.Clear();
            var currentRow = new ObservableCollection<JustifiedItem>();
            double currentWidth = 0;
            double targetHeight = JustifiedRowHeight;

            if (Assets == null) return;
            foreach (var item in Assets)
            {
                // best-effort aspect estimation: fallback to square if unknown
                double aspect = 1.0;
                try
                {
                    // attempt to parse WxH from thumbnail URL like .../160x120
                    var m = System.Text.RegularExpressions.Regex.Match(item.ThumbnailPath ?? string.Empty, "(\\d+)x(\\d+)");
                    if (m.Success)
                    {
                        double w = double.Parse(m.Groups[1].Value);
                        double h = double.Parse(m.Groups[2].Value);
                        if (h > 0) aspect = w / h;
                    }
                }
                catch { }

                double width = aspect * targetHeight;
                if (currentRow.Count > 0 && currentWidth + width + horizontalSpacing > containerWidth)
                {
                    // scale row to fit
                    double scale = (containerWidth - (currentRow.Count - 1) * horizontalSpacing) / currentWidth;
                    foreach (var ji in currentRow) ji.Width *= scale;
                    JustifiedRows.Add(currentRow);
                    currentRow = new ObservableCollection<JustifiedItem>();
                    currentWidth = 0;
                }

                currentRow.Add(new JustifiedItem { Source = item, Width = width });
                currentWidth += width;
            }

            if (currentRow.Count > 0) JustifiedRows.Add(currentRow);
        }

        private void ApplyTagFilter()
        {
            try
            {
                var selectedExts = _tagFilterVm?.GetSelectedExtensions();
                // If no selected extensions, show all
                var toShow = new List<TemplateItem>();
                if (selectedExts == null || selectedExts.Count == 0)
                {
                    toShow = _allAssets.ToList();
                }
                else
                {
                    foreach (var a in _allAssets)
                    {
                        var ext = Path.GetExtension(a.Path ?? string.Empty).TrimStart('.').ToLowerInvariant();
                        if (selectedExts.Contains(ext)) toShow.Add(a);
                    }
                }

                // Update Assets collection with diff to minimize UI churn
                var toRemove = Assets.Except(toShow).ToList();
                var toAdd = toShow.Except(Assets).ToList();

                foreach (var r in toRemove) Assets.Remove(r);
                foreach (var a in toAdd) Assets.Add(a);
            }
            catch { }
        }
    }
}
