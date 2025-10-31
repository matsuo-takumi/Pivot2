using System;
using System.Collections.Generic;

namespace Pivot.Models
{
	public class CustomFilter
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public string Name { get; set; } = string.Empty;
		public List<string> AllowedExtensions { get; set; } = new List<string>();
		public bool IsBuiltIn { get; set; } = false;
		// If true, represents an 'All' filter that disables other filtering when selected
		public bool IsAll { get; set; } = false;
		public int SortOrder { get; set; } = 0;
	}
}
