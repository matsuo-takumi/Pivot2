using System;
using System.Collections.Generic;

namespace Pivot.Models
{
	public class CodeCategory
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public string Name { get; set; } = string.Empty;
		public List<Guid> FilterIds { get; set; } = new List<Guid>();
		public int SortOrder { get; set; } = 0;
	}
}


