using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Pivot.Engine.Data;
using Pivot.Engine.Models;

namespace Pivot.Engine.Services
{
    public class AnalysisQueryService : IAnalysisQueryService
    {
        private readonly IDbContextFactory<PivotDbContext> _dbFactory;

        public AnalysisQueryService(IDbContextFactory<PivotDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<List<AssetEntity>> SearchByColorAsync(byte r, byte g, byte b, int tolerance = 30, int maxResults = 100)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            // 1. Coarse filtering in DB (R, G, B within tolerance)
            // Note: This is an approximation. A color might be close in Euclidean distance but outside strict R/G/B bounds if diagonally close.
            // But for performance, bounding box is good enough for initial cull.
            int minR = Math.Max(0, r - tolerance);
            int maxR = Math.Min(255, r + tolerance);
            int minG = Math.Max(0, g - tolerance);
            int maxG = Math.Min(255, g + tolerance);
            int minB = Math.Max(0, b - tolerance);
            int maxB = Math.Min(255, b + tolerance);

            var candidates = await context.AssetColors
                .Where(c => c.R >= minR && c.R <= maxR &&
                            c.G >= minG && c.G <= maxG &&
                            c.B >= minB && c.B <= maxB)
                .Select(c => new { c.AssetId, c.R, c.G, c.B, c.Ratio })
                .ToListAsync();

            // 2. Fine-grained sorting in memory
            var results = candidates
                .Select(c => new
                {
                    c.AssetId,
                    Distance = Math.Sqrt(Math.Pow(c.R - r, 2) + Math.Pow(c.G - g, 2) + Math.Pow(c.B - b, 2)),
                    c.Ratio
                })
                .OrderBy(x => x.Distance)
                .ThenByDescending(x => x.Ratio) // Prefer dominant colors
                .GroupBy(x => x.AssetId) // Dedup assets (if multiple colors match)
                .Take(maxResults)
                .Select(x => x.Key)
                .ToList();

            if (results.Count == 0) return new List<AssetEntity>();

            // 3. Fetch full entities
            return await context.Assets
                .Where(a => results.Contains(a.Id))
                .ToListAsync(); // Order might be lost here, client should sort or we fetch and re-sort
            
            // Re-sorting in client recommended or:
            // var entities = await ...
            // return entities.OrderBy(e => results.IndexOf(e.Id)).ToList();
        }

        public async Task<List<List<AssetEntity>>> FindDuplicatesAsync(int threshold = 0)
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            // Load all hashes (Id, Hash)
            // Memory Usage: 100k items * 16 bytes approx = 1.6MB. Safe.
            var allHashes = await context.Assets
                .Where(a => a.PerceptualHash.HasValue && !a.IsDeleted)
                .Select(a => new { a.Id, Hash = a.PerceptualHash!.Value })
                .ToListAsync();

            var groups = new List<List<int>>();
            var processed = new HashSet<int>();

            // Naive O(N^2) checks for N=100k is too slow (10 billion ops).
            // Optimization for Exact Match (threshold=0):
            if (threshold == 0)
            {
                var exactGroups = allHashes
                    .GroupBy(x => x.Hash)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Select(x => x.Id).ToList())
                    .ToList();
                
                // Fetch entities
                return await FetchGroupsAsync(context, exactGroups);
            }

            // For Near Duplicate (threshold > 0):
            // BK-Tree is ideal, but for < 100k items, simple partitioning might work.
            // Or grouping by 16-bit prefix? 
            // For now, let's implement Exact Match logic primarily, as threshold > 0 is expensive without specialized structure.
            // However, simpler approach:
            // Sort by Hash. Compare neighbors? No, hamming distance doesn't respect sort order linearly.
            
            // If user asks for threshold=3, we might restrict it to small sets or background job.
            // Let's implement exact match optimization first, and slow check for threshold > 0 if N is small (< 10k).
            // If N > 10k, warn or limit?
            
            if (allHashes.Count > 10000 && threshold > 0)
            {
                // Fallback to exact match to avoid freeze
                return await FetchGroupsAsync(context, allHashes
                    .GroupBy(x => x.Hash)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Select(x => x.Id).ToList())
                    .ToList());
            }
            
            // Slow O(N^2) for small sets
            for (int i = 0; i < allHashes.Count; i++)
            {
                if (processed.Contains(allHashes[i].Id)) continue;
                
                var currentGroup = new List<int> { allHashes[i].Id };
                processed.Add(allHashes[i].Id);

                for (int j = i + 1; j < allHashes.Count; j++)
                {
                    if (processed.Contains(allHashes[j].Id)) continue;

                    var dist = BitOperations.PopCount(allHashes[i].Hash ^ allHashes[j].Hash);
                    if (dist <= threshold)
                    {
                        currentGroup.Add(allHashes[j].Id);
                        processed.Add(allHashes[j].Id);
                    }
                }

                if (currentGroup.Count > 1)
                {
                    groups.Add(currentGroup);
                }
            }

            return await FetchGroupsAsync(context, groups);
        }

        private async Task<List<List<AssetEntity>>> FetchGroupsAsync(PivotDbContext context, List<List<int>> groups)
        {
            var result = new List<List<AssetEntity>>();
            var allIds = groups.SelectMany(g => g).Distinct().ToList();
            
            var entities = await context.Assets
                .Where(a => allIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id);

            foreach (var g in groups)
            {
                var list = new List<AssetEntity>();
                foreach (var id in g)
                {
                    if (entities.TryGetValue(id, out var e))
                    {
                        list.Add(e);
                    }
                }
                if (list.Count > 1) result.Add(list);
            }
            return result;
        }
    }
}
