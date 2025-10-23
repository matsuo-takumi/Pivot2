using System;
using System.Collections.Generic;

namespace Pivot.Models
{
	public class AssetMeta
	{
		public string Schema { get; set; } = "pivot.meta/v1";
		public string? Id { get; set; } // hash or stable id
		public List<string> UserTags { get; set; } = new List<string>();
		public int? Rating { get; set; }
		public string? Notes { get; set; }
		public string? CategoryOverride { get; set; }
		public Dictionary<string, object> Custom { get; set; } = new Dictionary<string, object>();
		public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
	}
}


