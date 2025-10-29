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

        public ObservableCollection<ObservableCollection<TemplateItem>> MasonryColumns { get; } = new ObservableCollection<ObservableCollection<TemplateItem>>();
        public ObservableCollection<ObservableCollection<JustifiedItem>> JustifiedRows { get; } = new ObservableCollection<ObservableCollection<JustifiedItem>>();

        public double JustifiedRowHeight { get; set; } = 140;

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

            BuildMasonryColumns();
            UseTextListMode = _currentLayout == LayoutType.List;
            ShowThumbnails = !UseTextListMode;
        }

        partial void OnCurrentLayoutChanged(LayoutType value)
        {
            UseTextListMode = value == LayoutType.List;
            ShowThumbnails = !UseTextListMode;
        }

        public async Task LoadFromDirectoriesAsync(IEnumerable<string> directories, int maxFiles = 200)
        {
            if (directories == null) return;
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase){ ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga", ".tif", ".tiff", ".webp" };
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
                var item = new TemplateItem
                {
                    Kind = AssetKind.Other,
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

        public void BuildMasonryColumns()
        {
            MasonryColumns.Clear();
            for (int i = 0; i < MasonryColumnCount; i++)
            {
                MasonryColumns.Add(new ObservableCollection<TemplateItem>());
            }

            var columnHeights = new int[MasonryColumnCount];
            foreach (var item in Assets)
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

        public void BuildJustifiedRows(double containerWidth, double horizontalSpacing)
        {
            if (containerWidth <= 0) return;
            JustifiedRows.Clear();
            double currentRowWidth = 0;
            var currentRow = new ObservableCollection<JustifiedItem>();
            double targetHeight = JustifiedRowHeight;

            foreach (var item in Assets)
            {
                double aspect = EstimateAspectFromUrl(item.ThumbnailPath);
                double width = aspect * targetHeight;
                if (currentRow.Count > 0 && currentRowWidth + width + horizontalSpacing > containerWidth)
                {
                    double scale = (containerWidth - (currentRow.Count - 1) * horizontalSpacing) / currentRowWidth;
                    foreach (var ji in currentRow)
                    {
                        ji.Width *= scale;
                    }
                    JustifiedRows.Add(currentRow);
                    currentRow = new ObservableCollection<JustifiedItem>();
                    currentRowWidth = 0;
                }

                currentRow.Add(new JustifiedItem { Source = item, Width = width });
                currentRowWidth += width;
            }

            if (currentRow.Count > 0)
            {
                JustifiedRows.Add(currentRow);
            }
        }

        private static double EstimateAspectFromUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return 200.0 / 180.0;
            var m = Regex.Match(url, "(\\d+)x(\\d+)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int w) && int.TryParse(m.Groups[2].Value, out int h) && h > 0)
            {
                return (double)w / h;
            }
            return 200.0 / 180.0;
        }

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
