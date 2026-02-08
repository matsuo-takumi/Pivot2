using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Pivot.Engine.Models;

namespace Pivot.Models
{
	public sealed class TemplateItem : ObservableObject
	{
		public AssetKind Kind { get; set; } = AssetKind.Other;
		public string Path { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public long Size { get; set; }
		public DateTime LastModified { get; set; }

		public double AspectRatio { get; set; } = 1.0;
		public int PixelWidth { get; set; }
		public int PixelHeight { get; set; }

		private string? _thumbnailPath;
		public string? ThumbnailPath
		{
			get => _thumbnailPath;
			set => SetProperty(ref _thumbnailPath, value);
		}

		private bool _isSelected;
		public bool IsSelected
		{
			get => _isSelected;
			set => SetProperty(ref _isSelected, value);
		}
	}
}


