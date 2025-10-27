using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Pivot.Models
{
	public class ImageItem : ObservableObject
	{
		public string Path { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Id { get; set; } = string.Empty;
		public long Size { get; set; }
		public DateTime LastModified { get; set; }

		private string? _thumbnailPath;
		public string? ThumbnailPath
		{
			get => _thumbnailPath;
			set => SetProperty(ref _thumbnailPath, value);
		}
	}
}


