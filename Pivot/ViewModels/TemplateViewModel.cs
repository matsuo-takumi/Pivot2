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
    public enum LayoutType
    {
        List = 0,
        Grid = 1,
        Masonry = 2,
        Flex = 3,
        Flow = 4
    }

// ... existing code ...

    public partial class TemplateViewModel : ObservableObject
	{
        public ObservableCollection<TemplateItem> TestItems { get; set; }

		// Masonry layout columns: each inner collection represents a vertical column
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
            // Cycle through the first three layouts: List(0) -> Grid(1) -> Masonry(2) -> List
            CurrentLayout = (LayoutType)(((int)CurrentLayout + 1) % 3);
        }

        private System.Threading.CancellationTokenSource? _loadCts;

        public TemplateViewModel()
		{
            TestItems = new ObservableCollection<TemplateItem>
            {
                new TemplateItem { Name = "Test Image 1", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/160x120?text=Image+1" },
                new TemplateItem { Name = "Test Image 2", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/300x260?text=Image+2" },
                new TemplateItem { Name = "Test Image 3", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/200x180?text=Image+3" },
                new TemplateItem { Name = "Test Image 4", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/400x140?text=Image+4" },
                new TemplateItem { Name = "Test Image 5", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/120x200?text=Image+5" },
                new TemplateItem { Name = "Test Image 6", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/300x300?text=Image+6" },
                new TemplateItem { Name = "Test Image 7", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/180x280?text=Image+7" },
                new TemplateItem { Name = "Test Image 8", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/240x160?text=Image+8" },
                new TemplateItem { Name = "Test Image 9", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/220x180?text=Image+9" },
                new TemplateItem { Name = "Test Image 10", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/200x220?text=Image+10" },
                new TemplateItem { Name = "Test Image 11", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/150x300?text=Image+11" },
                new TemplateItem { Name = "Test Image 12", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/260x120?text=Image+12" },
                new TemplateItem { Name = "Test Image 13", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/170x240?text=Image+13" },
                new TemplateItem { Name = "Test Image 14", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/210x140?text=Image+14" },
                new TemplateItem { Name = "Test Image 15", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/280x200?text=Image+15" },
                new TemplateItem { Name = "Test Image 16", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/160x300?text=Image+16" },
                new TemplateItem { Name = "Test Image 17", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/230x170?text=Image+17" },
                new TemplateItem { Name = "Test Image 18", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/190x230?text=Image+18" },
                new TemplateItem { Name = "Test Image 19", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/250x200?text=Image+19" },
                new TemplateItem { Name = "Test Image 20", Kind = AssetKind.Image, ThumbnailPath = "https://via.placeholder.com/170x300?text=Image+20" }
            };

			// initialize masonry columns
			BuildMasonryColumns();

			// ensure derived flags
			UseTextListMode = _currentLayout == LayoutType.List;
			ShowThumbnails = !UseTextListMode;
		}

		partial void OnCurrentLayoutChanged(LayoutType value)
		{
			UseTextListMode = value == LayoutType.List;
			ShowThumbnails = !UseTextListMode;
		}

		/// <summary>
		/// Load image file paths from given directories and populate TestItems for testing.
		/// This is intended for local testing only (no thumbnail generation).
		/// </summary>
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

            // cancel previous load if any
            try { _loadCts?.Cancel(); } catch { }
            _loadCts = new System.Threading.CancellationTokenSource();
            var ct = _loadCts.Token;

            // convert to TemplateItem entries (store original path in Path property)
            TestItems.Clear();
            var thumbService = App.Current.Services.GetService<Pivot.Services.IThumbnailService>();
            // pre-initialize service if available
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
                    Kind = AssetKind.Image,
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
                TestItems.Add(item);
            }

            BuildMasonryColumns();

            // Start background thumbnail generation using the registered IThumbnailService.
            try
            {
                var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                if (thumbService == null) return;

                var tasks = new List<Task>();
                // create thumbnails concurrently (service has internal parallelism control)
                foreach (var item in TestItems.ToList())
                {
                    var originalPath = item.Path;
                    if (string.IsNullOrWhiteSpace(originalPath) || !File.Exists(originalPath)) continue;
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            // target size reasonable for grid thumbnails
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
			// height-aware distribution using approximate heights parsed from URL (px)
			MasonryColumns.Clear();
			for (int i = 0; i < MasonryColumnCount; i++)
			{
                MasonryColumns.Add(new ObservableCollection<TemplateItem>());
			}

			var columnHeights = new int[MasonryColumnCount];
			foreach (var item in TestItems)
			{
				int h = EstimateHeightFromUrl(item.ThumbnailPath);
				// choose column with minimal current height
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
