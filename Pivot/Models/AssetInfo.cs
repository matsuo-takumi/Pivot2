using System;
using System.Collections.Generic;

namespace Pivot.Models
{
	public class AssetInfo
	{
		public string Path { get; set; } = string.Empty;
		public string Category { get; set; } = string.Empty;
		public string Type { get; set; } = string.Empty; // file extension or mime-like token
		public List<string> Tags { get; set; } = new List<string>();
		public DateTime LastModified { get; set; }
		public long Size { get; set; }
		public string? Hash { get; set; }
	}
}


