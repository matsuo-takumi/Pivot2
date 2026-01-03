using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pivot.Data;
using Pivot.Models;

namespace Pivot.Services
{
    /// <summary>
    /// Unified query service for assets with filtering, sorting, and pagination.
    /// Designed for high performance with large datasets (10k+ items).
    /// </summary>
    public class AssetQueryService
    {
        private readonly ILogger<AssetQueryService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public AssetQueryService(
            ILogger<AssetQueryService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Query assets with filter criteria and pagination.
        /// </summary>
        /// <param name="criteria">Filter and sort criteria</param>
        /// <param name="skip">Number of items to skip (for pagination)</param>
        /// <param name="take">Number of items to take (page size)</param>
        /// <returns>List of matching assets</returns>
        public async Task<List<AssetEntity>> QueryAsync(FilterCriteria criteria, int skip, int take)
        {
            using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .CreateScope(_serviceProvider);
            var db = scope.ServiceProvider.GetRequiredService<PivotDbContext>();

            var query = BuildQuery(db, criteria);
            
            var results = await query
                .Skip(skip)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();

            _logger.LogDebug("AssetQueryService: Query returned {Count} items (skip={Skip}, take={Take})", 
                results.Count, skip, take);

            return results;
        }

        /// <summary>
        /// Count total matching assets for pagination.
        /// </summary>
        public async Task<int> CountAsync(FilterCriteria criteria)
        {
            using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .CreateScope(_serviceProvider);
            var db = scope.ServiceProvider.GetRequiredService<PivotDbContext>();

            var query = BuildQuery(db, criteria);
            var count = await query.CountAsync();

            _logger.LogDebug("AssetQueryService: Count = {Count}", count);
            return count;
        }

        /// <summary>
        /// Build a queryable with applied filters and sorting.
        /// This is the core query engine.
        /// </summary>
        public IQueryable<AssetEntity> BuildQuery(PivotDbContext db, FilterCriteria criteria)
        {
            IQueryable<AssetEntity> query = db.Assets;

            // Soft delete filter
            if (!criteria.IncludeDeleted)
            {
                query = query.Where(a => !a.IsDeleted);
            }

            // Kind filter
            if (criteria.TargetKind.HasValue)
            {
                query = query.Where(a => a.Kind == criteria.TargetKind.Value);
            }

            // Directory filter (match exact directory or subdirectories)
            if (!string.IsNullOrWhiteSpace(criteria.Directory))
            {
                var normalizedDir = criteria.Directory.Replace('/', '\\').TrimEnd('\\');
                // Use LIKE for SQLite compatibility (StartsWith can have case-sensitivity issues)
                var dirPattern = normalizedDir + "\\%";
                System.Diagnostics.Debug.WriteLine($"[AssetQueryService] Directory filter: criteria='{criteria.Directory}', normalized='{normalizedDir}', pattern='{dirPattern}'");
                query = query.Where(a => 
                    a.Directory == normalizedDir ||
                    EF.Functions.Like(a.Directory, dirPattern));
            }

            // File name search (LIKE pattern)
            if (!string.IsNullOrWhiteSpace(criteria.SearchQuery))
            {
                var searchPattern = $"%{criteria.SearchQuery}%";
                query = query.Where(a => EF.Functions.Like(a.FileName, searchPattern));
            }

            // Tag search using LIKE on JSON field
            // Performance note: For very large datasets, consider raw SQL with SQLite JSON functions
            if (criteria.Tags != null && criteria.Tags.Count > 0)
            {
                if (criteria.TagMode == TagMatchMode.Any)
                {
                    // OR: Any tag matches
                    foreach (var tag in criteria.Tags)
                    {
                        var tagPattern = $"%\"{tag}\"%";
                        query = query.Where(a => a.UserTagsJson != null && 
                            EF.Functions.Like(a.UserTagsJson, tagPattern));
                        break; // For OR, we need at least one - complex OR requires different approach
                    }
                }
                else
                {
                    // AND: All tags must match
                    foreach (var tag in criteria.Tags)
                    {
                        var tagPattern = $"%\"{tag}\"%";
                        query = query.Where(a => a.UserTagsJson != null && 
                            EF.Functions.Like(a.UserTagsJson, tagPattern));
                    }
                }
            }

            // Aspect ratio filter
            if (criteria.MinAspectRatio.HasValue)
            {
                query = query.Where(a => a.AspectRatio >= criteria.MinAspectRatio.Value);
            }
            if (criteria.MaxAspectRatio.HasValue)
            {
                query = query.Where(a => a.AspectRatio <= criteria.MaxAspectRatio.Value);
            }

            // Rating filter
            if (criteria.MinRating > 0)
            {
                query = query.Where(a => a.Rating >= criteria.MinRating);
            }

            // Apply sorting
            query = ApplySorting(query, criteria.SortField, criteria.SortDirection);

            return query;
        }

        /// <summary>
        /// Apply dynamic sorting based on SortField and SortDirection.
        /// Uses indexed columns for performance.
        /// </summary>
        private IQueryable<AssetEntity> ApplySorting(
            IQueryable<AssetEntity> query, 
            SortField field, 
            SortDirection direction)
        {
            bool ascending = direction == SortDirection.Ascending;

            return field switch
            {
                SortField.Name => ascending 
                    ? query.OrderBy(a => a.FileName) 
                    : query.OrderByDescending(a => a.FileName),
                    
                SortField.Date => ascending 
                    ? query.OrderBy(a => a.LastModifiedUtc) 
                    : query.OrderByDescending(a => a.LastModifiedUtc),
                    
                SortField.Size => ascending 
                    ? query.OrderBy(a => a.FileSize) 
                    : query.OrderByDescending(a => a.FileSize),
                    
                SortField.Type => ascending 
                    ? query.OrderBy(a => a.Extension) 
                    : query.OrderByDescending(a => a.Extension),
                    
                SortField.Rating => ascending 
                    ? query.OrderBy(a => a.Rating) 
                    : query.OrderByDescending(a => a.Rating),
                    
                SortField.AspectRatio => ascending 
                    ? query.OrderBy(a => a.AspectRatio) 
                    : query.OrderByDescending(a => a.AspectRatio),
                    
                _ => query.OrderByDescending(a => a.LastModifiedUtc) // Default
            };
        }
    }
}
