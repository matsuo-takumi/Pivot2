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

namespace Pivot.ViewModels
{
	public enum LayoutType
	{
		List = 0,
		Grid = 1,
		Masonry = 2,
		Justified = 3,
		Flex = 4,
		Flow = 5
	}

    public class JustifiedItem
    {
        public ImageItem Source { get; set; } = new ImageItem();
        public double Width { get; set; }
    }

    public partial class TemplateViewModel : ObservableObject
	{
		public ObservableCollection<ImageItem> TestItems { get; set; }

		// Masonry layout columns: each inner collection represents a vertical column
		public ObservableCollection<ObservableCollection<ImageItem>> MasonryColumns { get; } = new ObservableCollection<ObservableCollection<ImageItem>>();

		// Justified layout: collection of rows, each row has items with computed width
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

		[RelayCommand]
		private void ToggleLayout()
		{
			// Cycle through the first four layouts: List(0) -> Grid(1) -> Masonry(2) -> Justified(3) -> List
			CurrentLayout = (LayoutType)(((int)CurrentLayout + 1) % 4);
		}

		public TemplateViewModel()
		{
            TestItems = new ObservableCollection<ImageItem>
            {
                new ImageItem { Id = "1", Name = "Test Image 1", ThumbnailPath = "https://via.placeholder.com/160x120?text=Image+1" },
                new ImageItem { Id = "2", Name = "Test Image 2", ThumbnailPath = "https://via.placeholder.com/300x260?text=Image+2" },
                new ImageItem { Id = "3", Name = "Test Image 3", ThumbnailPath = "https://via.placeholder.com/200x180?text=Image+3" },
                new ImageItem { Id = "4", Name = "Test Image 4", ThumbnailPath = "https://via.placeholder.com/400x140?text=Image+4" },
                new ImageItem { Id = "5", Name = "Test Image 5", ThumbnailPath = "https://via.placeholder.com/120x200?text=Image+5" },
                new ImageItem { Id = "6", Name = "Test Image 6", ThumbnailPath = "https://via.placeholder.com/300x300?text=Image+6" },
                new ImageItem { Id = "7", Name = "Test Image 7", ThumbnailPath = "https://via.placeholder.com/180x280?text=Image+7" },
                new ImageItem { Id = "8", Name = "Test Image 8", ThumbnailPath = "https://via.placeholder.com/240x160?text=Image+8" },
                new ImageItem { Id = "9", Name = "Test Image 9", ThumbnailPath = "https://via.placeholder.com/220x180?text=Image+9" },
                new ImageItem { Id = "10", Name = "Test Image 10", ThumbnailPath = "https://via.placeholder.com/200x220?text=Image+10" },
                new ImageItem { Id = "11", Name = "Test Image 11", ThumbnailPath = "https://via.placeholder.com/150x300?text=Image+11" },
                new ImageItem { Id = "12", Name = "Test Image 12", ThumbnailPath = "https://via.placeholder.com/260x120?text=Image+12" },
                new ImageItem { Id = "13", Name = "Test Image 13", ThumbnailPath = "https://via.placeholder.com/170x240?text=Image+13" },
                new ImageItem { Id = "14", Name = "Test Image 14", ThumbnailPath = "https://via.placeholder.com/210x140?text=Image+14" },
                new ImageItem { Id = "15", Name = "Test Image 15", ThumbnailPath = "https://via.placeholder.com/280x200?text=Image+15" },
                new ImageItem { Id = "16", Name = "Test Image 16", ThumbnailPath = "https://via.placeholder.com/160x300?text=Image+16" },
                new ImageItem { Id = "17", Name = "Test Image 17", ThumbnailPath = "https://via.placeholder.com/230x170?text=Image+17" },
                new ImageItem { Id = "18", Name = "Test Image 18", ThumbnailPath = "https://via.placeholder.com/190x230?text=Image+18" },
                new ImageItem { Id = "19", Name = "Test Image 19", ThumbnailPath = "https://via.placeholder.com/250x200?text=Image+19" },
                new ImageItem { Id = "20", Name = "Test Image 20", ThumbnailPath = "https://via.placeholder.com/170x300?text=Image+20" }
            };

			// initialize masonry columns
			BuildMasonryColumns();
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
			// convert to ImageItem entries
			TestItems.Clear();
			int id = 1;
			foreach (var f in files)
			{
				// convert to file:// uri for Image.Source
				var uri = new System.Uri(f).AbsoluteUri; // ensures proper file:/// scheme
				TestItems.Add(new ImageItem { Id = id.ToString(), Name = Path.GetFileName(f), ThumbnailPath = uri });
				id++;
			}

			BuildMasonryColumns();
		}

		public void BuildMasonryColumns()
		{
			// height-aware distribution using approximate heights parsed from URL (px)
			MasonryColumns.Clear();
			for (int i = 0; i < MasonryColumnCount; i++)
			{
				MasonryColumns.Add(new ObservableCollection<ImageItem>());
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

		public void BuildJustifiedRows(double containerWidth, double horizontalSpacing)
		{
			if (containerWidth <= 0) return;
			JustifiedRows.Clear();
			double currentRowWidth = 0;
			var currentRow = new ObservableCollection<JustifiedItem>();
			double targetHeight = JustifiedRowHeight;

			foreach (var item in TestItems)
			{
				double aspect = EstimateAspectFromUrl(item.ThumbnailPath); // width/height
				double width = aspect * targetHeight;
				if (currentRow.Count > 0 && currentRowWidth + width + horizontalSpacing > containerWidth)
				{
					// scale row to fit container
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
				// last row: no scaling (or optionally stretch)
				JustifiedRows.Add(currentRow);
			}
		}

        private static double EstimateAspectFromUrl(string? url)
        {
            // extract WxH -> aspect = W/H
            if (string.IsNullOrEmpty(url)) return 200.0 / 180.0;
            var m = Regex.Match(url, "(\\d+)x(\\d+)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int w) && int.TryParse(m.Groups[2].Value, out int h) && h > 0)
            {
                return (double)w / h;
            }
            return 200.0 / 180.0;
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
