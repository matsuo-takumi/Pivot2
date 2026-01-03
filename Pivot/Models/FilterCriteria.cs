using System.Collections.Generic;

namespace Pivot.Models
{
    /// <summary>
    /// Tag matching mode for filter queries.
    /// </summary>
    public enum TagMatchMode
    {
        /// <summary>Any tag matches (OR logic)</summary>
        Any,
        /// <summary>All tags must match (AND logic)</summary>
        All
    }

    /// <summary>
    /// Unified filter criteria for querying assets across all tabs.
    /// Used by AssetQueryService for building dynamic queries.
    /// </summary>
    public class FilterCriteria
    {
        /// <summary>
        /// Filter by asset kind (Image, Code, etc). Null = all kinds.
        /// </summary>
        public AssetKind? TargetKind { get; set; }

        /// <summary>
        /// File name search query (LIKE pattern).
        /// </summary>
        public string? SearchQuery { get; set; }

        /// <summary>
        /// Directory path filter. Null = all directories.
        /// </summary>
        public string? Directory { get; set; }

        /// <summary>
        /// Tags to filter by. Null or empty = no tag filter.
        /// </summary>
        public HashSet<string>? Tags { get; set; }

        /// <summary>
        /// How to match tags (Any = OR, All = AND).
        /// </summary>
        public TagMatchMode TagMode { get; set; } = TagMatchMode.Any;

        /// <summary>
        /// Minimum aspect ratio filter. Null = no minimum.
        /// </summary>
        public double? MinAspectRatio { get; set; }

        /// <summary>
        /// Maximum aspect ratio filter. Null = no maximum.
        /// </summary>
        public double? MaxAspectRatio { get; set; }

        /// <summary>
        /// Minimum rating filter (0-5). Default 0 = no filter.
        /// </summary>
        public int MinRating { get; set; } = 0;

        /// <summary>
        /// Field to sort by.
        /// </summary>
        public Pivot.Services.SortField SortField { get; set; } = Pivot.Services.SortField.Date;

        /// <summary>
        /// Sort direction.
        /// </summary>
        public Pivot.Services.SortDirection SortDirection { get; set; } = Pivot.Services.SortDirection.Descending;

        /// <summary>
        /// Include soft-deleted items. Default false.
        /// </summary>
        public bool IncludeDeleted { get; set; } = false;

        /// <summary>
        /// Creates a copy of this criteria.
        /// </summary>
        public FilterCriteria Clone()
        {
            return new FilterCriteria
            {
                TargetKind = TargetKind,
                SearchQuery = SearchQuery,
                Directory = Directory,
                Tags = Tags != null ? new HashSet<string>(Tags) : null,
                TagMode = TagMode,
                MinAspectRatio = MinAspectRatio,
                MaxAspectRatio = MaxAspectRatio,
                MinRating = MinRating,
                SortField = SortField,
                SortDirection = SortDirection,
                IncludeDeleted = IncludeDeleted
            };
        }
    }
}
