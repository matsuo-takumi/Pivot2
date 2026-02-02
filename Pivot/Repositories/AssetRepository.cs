using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pivot.Data;
using Pivot.Models;

namespace Pivot.Repositories
{
    public class AssetRepository : IAssetRepository
    {
        private readonly PivotDbContext _context;

        public AssetRepository(PivotDbContext context)
        {
            _context = context;
        }

        public async Task<List<AssetEntity>> GetPagedAsync(
            AssetKind? kind = null,
            string? directory = null,
            string? sortField = "LastModifiedUtc",
            bool ascending = false,
            int skip = 0,
            int take = 100,
            CancellationToken ct = default)
        {
            var query = _context.Assets.AsQueryable();

            if (kind.HasValue)
                query = query.Where(a => a.Kind == kind.Value);

            if (!string.IsNullOrEmpty(directory))
                query = query.Where(a => a.Directory.StartsWith(directory));

            // ソート
            query = (sortField?.ToLower(), ascending) switch
            {
                ("name", true) => query.OrderBy(a => a.FileName),
                ("name", false) => query.OrderByDescending(a => a.FileName),
                ("size", true) => query.OrderBy(a => a.FileSize),
                ("size", false) => query.OrderByDescending(a => a.FileSize),
                ("lastmodifiedutc", true) => query.OrderBy(a => a.LastModifiedUtc),
                _ => query.OrderByDescending(a => a.LastModifiedUtc)
            };

            return await query.Skip(skip).Take(take).ToListAsync(ct);
        }

        public async Task<int> GetCountAsync(
            AssetKind? kind = null,
            string? directory = null,
            CancellationToken ct = default)
        {
            var query = _context.Assets.AsQueryable();

            if (kind.HasValue)
                query = query.Where(a => a.Kind == kind.Value);

            if (!string.IsNullOrEmpty(directory))
                query = query.Where(a => a.Directory.StartsWith(directory));

            return await query.CountAsync(ct);
        }

        public async Task<AssetEntity?> GetByPathAsync(string filePath, CancellationToken ct = default)
        {
            return await _context.Assets
                .FirstOrDefaultAsync(a => a.FilePath == filePath, ct);
        }

        public async Task UpsertAsync(AssetEntity asset, CancellationToken ct = default)
        {
            // Use ExecuteUpdateAsync for single round-trip update (EF Core 7+)
            // This avoids SELECT + UPDATE pattern, improving performance significantly
            var affected = await _context.Assets
                .Where(a => a.FilePath == asset.FilePath)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(a => a.FileName, asset.FileName)
                    .SetProperty(a => a.Directory, asset.Directory)
                    .SetProperty(a => a.Extension, asset.Extension)
                    .SetProperty(a => a.FileSize, asset.FileSize)
                    .SetProperty(a => a.LastModifiedUtc, asset.LastModifiedUtc)
                    .SetProperty(a => a.Hash, asset.Hash)
                    .SetProperty(a => a.Kind, asset.Kind)
                    .SetProperty(a => a.Width, asset.Width)
                    .SetProperty(a => a.Height, asset.Height)
                    .SetProperty(a => a.AspectRatio, asset.AspectRatio)
                    .SetProperty(a => a.ThumbnailPath, asset.ThumbnailPath)
                    .SetProperty(a => a.ThumbnailGeneratedAt, asset.ThumbnailGeneratedAt)
                    .SetProperty(a => a.Language, asset.Language)
                    .SetProperty(a => a.Tool, asset.Tool)
                    .SetProperty(a => a.ContentIndex, asset.ContentIndex)
                    .SetProperty(a => a.UserTagsJson, asset.UserTagsJson)
                    .SetProperty(a => a.SortOrder, asset.SortOrder)
                    .SetProperty(a => a.UpdatedAt, DateTime.UtcNow),
                    ct);

            // If no rows were updated, insert new record
            if (affected == 0)
            {
                asset.CreatedAt = DateTime.UtcNow;
                asset.UpdatedAt = DateTime.UtcNow;
                _context.Assets.Add(asset);
                await _context.SaveChangesAsync(ct);
            }
        }

        public async Task MarkDeletedAsync(string filePath, CancellationToken ct = default)
        {
            var asset = await GetByPathAsync(filePath, ct);
            if (asset != null)
            {
                asset.IsDeleted = true;
                asset.DeletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
            }
        }

        public async Task<List<string>> GetAllDirectoriesAsync(
            AssetKind? kind = null,
            CancellationToken ct = default)
        {
            var query = _context.Assets.AsQueryable();

            if (kind.HasValue)
                query = query.Where(a => a.Kind == kind.Value);

            return await query
                .Select(a => a.Directory)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync(ct);
        }

        public async Task<int> DeleteByDirectoryAsync(string directoryPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                return 0;

            var normalizedDir = directoryPath.Replace('/', '\\').TrimEnd('\\');
            var dirPattern = normalizedDir + "\\%";

            // Delete assets where Directory matches exactly or is a subdirectory
            var assetsToDelete = await _context.Assets
                .Where(a => a.Directory == normalizedDir || 
                           a.Directory.StartsWith(normalizedDir + "\\"))
                .ToListAsync(ct);

            if (assetsToDelete.Count > 0)
            {
                _context.Assets.RemoveRange(assetsToDelete);
                await _context.SaveChangesAsync(ct);
            }

            return assetsToDelete.Count;
        }

        public async Task<Dictionary<string, AssetEntity>> GetExistingAssetsInDirectoryAsync(
            string directoryPath, 
            CancellationToken ct = default)
        {
            var normalizedDir = directoryPath.Replace('/', '\\').TrimEnd('\\');
            
            // Get all assets in directory and subdirectories
            var assets = await _context.Assets
                .AsNoTracking()  // Read-only for performance
                .Where(a => !a.IsDeleted && (a.Directory == normalizedDir || a.Directory.StartsWith(normalizedDir + "\\")))
                .ToListAsync(ct);

            // Build dictionary keyed by FilePath (case-insensitive for Windows)
            return assets.ToDictionary(
                a => a.FilePath, 
                a => a, 
                StringComparer.OrdinalIgnoreCase);
        }

        public async Task HardDeleteByPathAsync(string filePath, CancellationToken ct = default)
        {
            await _context.Assets
                .Where(a => a.FilePath == filePath)
                .ExecuteDeleteAsync(ct);
        }
    }
}
