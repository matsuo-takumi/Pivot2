using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pivot.CodeModule.Services;
using Pivot.Models;

namespace Pivot.Services
{
    /// <summary>
    /// IAssetRepositoryのEFCore実装。
    /// SQLiteデータベースからアセットメタデータを効率的に読み書きします。
    /// </summary>
    public class AssetRepository : IAssetRepository
    {
        private readonly IDbContextFactory<SQLiteDbContext> _contextFactory;
        private readonly ILogger<AssetRepository> _logger;

        public AssetRepository(
            IDbContextFactory<SQLiteDbContext> contextFactory,
            ILogger<AssetRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        // =============== Read Operations ===============

        public async Task<List<AssetFile>> GetAssetsAsync(
            IEnumerable<string>? directories = null,
            AssetKind? kind = null,
            int skip = 0,
            int take = 50,
            string orderBy = "FileName",
            bool descending = false,
            CancellationToken ct = default)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
            
            var query = ctx.AssetFiles.AsNoTracking().Where(a => !a.IsDeleted);

            // Directory filter (any directory that starts with the given paths)
            if (directories != null)
            {
                var dirList = directories.ToList();
                if (dirList.Count > 0)
                {
                    query = query.Where(a => dirList.Any(d => a.Directory.StartsWith(d)));
                }
            }

            // Kind filter
            if (kind.HasValue)
            {
                query = query.Where(a => a.Kind == kind.Value);
            }

            // Ordering
            query = orderBy switch
            {
                "LastModifiedTicks" => descending 
                    ? query.OrderByDescending(a => a.LastModifiedTicks) 
                    : query.OrderBy(a => a.LastModifiedTicks),
                "FileSize" => descending 
                    ? query.OrderByDescending(a => a.FileSize) 
                    : query.OrderBy(a => a.FileSize),
                "Kind" => descending 
                    ? query.OrderByDescending(a => a.Kind) 
                    : query.OrderBy(a => a.Kind),
                _ => descending 
                    ? query.OrderByDescending(a => a.FileName) 
                    : query.OrderBy(a => a.FileName)
            };

            return await query
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);
        }

        public async Task<int> GetCountAsync(
            IEnumerable<string>? directories = null,
            AssetKind? kind = null,
            CancellationToken ct = default)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
            
            var query = ctx.AssetFiles.AsNoTracking().Where(a => !a.IsDeleted);

            if (directories != null)
            {
                var dirList = directories.ToList();
                if (dirList.Count > 0)
                {
                    query = query.Where(a => dirList.Any(d => a.Directory.StartsWith(d)));
                }
            }

            if (kind.HasValue)
            {
                query = query.Where(a => a.Kind == kind.Value);
            }

            return await query.CountAsync(ct);
        }

        public async Task<AssetFile?> GetByPathAsync(string filePath, CancellationToken ct = default)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
            return await ctx.AssetFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.FilePath == filePath, ct);
        }

        public async Task<Dictionary<string, long>> GetFileMapAsync(
            IEnumerable<string> directories,
            CancellationToken ct = default)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
            
            var dirList = directories.ToList();
            
            var assets = await ctx.AssetFiles
                .AsNoTracking()
                .Where(a => !a.IsDeleted && dirList.Any(d => a.Directory.StartsWith(d)))
                .Select(a => new { a.FilePath, a.LastModifiedTicks })
                .ToListAsync(ct);

            return assets.ToDictionary(
                a => a.FilePath, 
                a => a.LastModifiedTicks, 
                StringComparer.OrdinalIgnoreCase);
        }

        public async Task<List<AssetFile>> GetUnindexedAssetsAsync(int take = 100, CancellationToken ct = default)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
            
            return await ctx.AssetFiles
                .Where(a => !a.IsDeleted && !a.IsIndexed)
                .OrderBy(a => a.Id)
                .Take(take)
                .ToListAsync(ct);
        }

        // =============== Write Operations ===============

        public async Task AddAsync(AssetFile asset, CancellationToken ct = default)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
            ctx.AssetFiles.Add(asset);
            await ctx.SaveChangesAsync(ct);
        }

        public async Task AddRangeAsync(IEnumerable<AssetFile> assets, CancellationToken ct = default)
        {
            var assetList = assets.ToList();
            if (assetList.Count == 0) return;

            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
            
            // Batch insert for performance
            const int batchSize = 100;
            for (int i = 0; i < assetList.Count; i += batchSize)
            {
                var batch = assetList.Skip(i).Take(batchSize);
                ctx.AssetFiles.AddRange(batch);
                await ctx.SaveChangesAsync(ct);
            }
            
            _logger.LogInformation("AssetRepository: Added {Count} assets", assetList.Count);
        }

        public async Task UpdateAsync(AssetFile asset, CancellationToken ct = default)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
            ctx.AssetFiles.Update(asset);
            await ctx.SaveChangesAsync(ct);
        }

        public async Task UpdateRangeAsync(IEnumerable<AssetFile> assets, CancellationToken ct = default)
        {
            var assetList = assets.ToList();
            if (assetList.Count == 0) return;

            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
            ctx.AssetFiles.UpdateRange(assetList);
            await ctx.SaveChangesAsync(ct);
            
            _logger.LogInformation("AssetRepository: Updated {Count} assets", assetList.Count);
        }

        public async Task DeleteAsync(string filePath, CancellationToken ct = default)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
            var asset = await ctx.AssetFiles.FirstOrDefaultAsync(a => a.FilePath == filePath, ct);
            if (asset != null)
            {
                ctx.AssetFiles.Remove(asset);
                await ctx.SaveChangesAsync(ct);
            }
        }

        public async Task DeleteRangeAsync(IEnumerable<string> filePaths, CancellationToken ct = default)
        {
            var pathList = filePaths.ToList();
            if (pathList.Count == 0) return;

            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
            
            var assets = await ctx.AssetFiles
                .Where(a => pathList.Contains(a.FilePath))
                .ToListAsync(ct);
            
            if (assets.Count > 0)
            {
                ctx.AssetFiles.RemoveRange(assets);
                await ctx.SaveChangesAsync(ct);
                _logger.LogInformation("AssetRepository: Deleted {Count} assets", assets.Count);
            }
        }

        public async Task MarkDeletedByDirectoryAsync(string directory, CancellationToken ct = default)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
            
            var assets = await ctx.AssetFiles
                .Where(a => a.Directory.StartsWith(directory) && !a.IsDeleted)
                .ToListAsync(ct);
            
            foreach (var asset in assets)
            {
                asset.IsDeleted = true;
            }
            
            await ctx.SaveChangesAsync(ct);
            _logger.LogInformation("AssetRepository: Marked {Count} assets as deleted in {Dir}", assets.Count, directory);
        }

        public async Task PurgeDeletedAsync(CancellationToken ct = default)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
            
            var deleted = await ctx.AssetFiles
                .Where(a => a.IsDeleted)
                .ToListAsync(ct);
            
            if (deleted.Count > 0)
            {
                ctx.AssetFiles.RemoveRange(deleted);
                await ctx.SaveChangesAsync(ct);
                _logger.LogInformation("AssetRepository: Purged {Count} deleted assets", deleted.Count);
            }
        }
    }
}
