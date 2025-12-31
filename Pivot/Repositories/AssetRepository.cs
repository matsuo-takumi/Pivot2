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
            var existing = await GetByPathAsync(asset.FilePath, ct);

            if (existing != null)
            {
                // Update
                existing.FileName = asset.FileName;
                existing.Directory = asset.Directory;
                existing.Extension = asset.Extension;
                existing.FileSize = asset.FileSize;
                existing.LastModifiedUtc = asset.LastModifiedUtc;
                existing.Hash = asset.Hash;
                existing.Kind = asset.Kind;
                existing.Width = asset.Width;
                existing.Height = asset.Height;
                existing.AspectRatio = asset.AspectRatio;
                existing.ThumbnailPath = asset.ThumbnailPath;
                existing.ThumbnailGeneratedAt = asset.ThumbnailGeneratedAt;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Insert
                asset.CreatedAt = DateTime.UtcNow;
                asset.UpdatedAt = DateTime.UtcNow;
                await _context.Assets.AddAsync(asset, ct);
            }

            await _context.SaveChangesAsync(ct);
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
    }
}
