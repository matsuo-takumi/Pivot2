using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pivot.Engine.Data;
using Pivot.Engine.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Pivot.Services
{
    /// <summary>
    /// Background service for extracting and indexing image metadata.
    /// Handles Width, Height, AspectRatio, and DominantColor extraction.
    /// Thread-safe: creates new DbContext scope per batch operation.
    /// </summary>
    public class MetadataIndexingService
    {
        private readonly ILogger<MetadataIndexingService> _logger;
        private readonly IServiceProvider _serviceProvider;
        
        private const int BatchSize = 100;
        private const int MaxDegreeOfParallelism = 4;
        private const int ThumbnailSizeForColorExtraction = 32;

        /// <summary>
        /// Supported image extensions for ImageSharp processing.
        /// Files with other extensions will be skipped to avoid unnecessary exceptions.
        /// </summary>
        private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp",
            ".webp", ".tga", ".tiff", ".tif", ".qoi", ".pbm"
        };

        public MetadataIndexingService(
            ILogger<MetadataIndexingService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Index all assets that are missing metadata (Width, Height, AspectRatio).
        /// Runs in background without blocking UI thread.
        /// </summary>
        public async Task IndexPendingAssetsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("MetadataIndexingService: Starting metadata indexing...");

            int totalProcessed = 0;
            int totalErrors = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                // Get batch of assets needing indexing
                List<int> pendingIds;
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<PivotDbContext>();
                    pendingIds = await db.Assets
                        .Where(a => !a.IsDeleted && 
                                   a.Kind == AssetKind.Image &&  // Only process images
                                   (a.Width == null || a.Height == null || a.AspectRatio == null))
                        .OrderBy(a => a.Id)
                        .Take(BatchSize)
                        .Select(a => a.Id)
                        .ToListAsync(cancellationToken);
                }

                if (pendingIds.Count == 0)
                {
                    _logger.LogInformation("MetadataIndexingService: No pending assets. Indexing complete. Total: {Total}, Errors: {Errors}", 
                        totalProcessed, totalErrors);
                    break;
                }

                // Process batch in parallel (compute only, not DB writes)
                var results = new ConcurrentBag<MetadataResult>();
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = MaxDegreeOfParallelism,
                    CancellationToken = cancellationToken
                };

                await Task.Run(() =>
                {
                    Parallel.ForEach(pendingIds, options, id =>
                    {
                        var result = ExtractMetadataForAsset(id);
                        if (result != null)
                        {
                            results.Add(result);
                        }
                    });
                }, cancellationToken);

                // Bulk update in single DbContext (thread-safe)
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<PivotDbContext>();
                    
                    foreach (var result in results)
                    {
                        var asset = await db.Assets.FindAsync(new object[] { result.AssetId }, cancellationToken);
                        if (asset != null)
                        {
                            asset.Width = result.Width;
                            asset.Height = result.Height;
                            asset.AspectRatio = result.AspectRatio;
                            if (!string.IsNullOrEmpty(result.DominantColor))
                            {
                                asset.DominantColor = result.DominantColor;
                            }
                        }
                    }

                    var saved = await db.SaveChangesAsync(cancellationToken);
                    totalProcessed += results.Count;
                    totalErrors += pendingIds.Count - results.Count;

                    _logger.LogDebug("MetadataIndexingService: Batch saved. Processed: {Count}, Total: {Total}", 
                        results.Count, totalProcessed);
                }
            }
        }

        /// <summary>
        /// Extract metadata for a single asset.
        /// Creates its own DbContext scope to avoid threading issues.
        /// </summary>
        private MetadataResult? ExtractMetadataForAsset(int assetId)
        {
            try
            {
                string? filePath;
                
                // Get file path in separate scope
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<PivotDbContext>();
                    filePath = db.Assets
                        .Where(a => a.Id == assetId)
                        .Select(a => a.FilePath)
                        .FirstOrDefault();
                }

                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return null;
                }

                // Check if file extension is supported by ImageSharp
                var extension = Path.GetExtension(filePath);
                if (!SupportedImageExtensions.Contains(extension))
                {
                    // Silently skip unsupported formats (no warning log)
                    return null;
                }

                // Extract image dimensions using ImageSharp (header only for performance)
                var info = Image.Identify(filePath);
                if (info == null)
                {
                    return null;
                }

                int width = info.Width;
                int height = info.Height;
                double aspectRatio = height > 0 ? (double)width / height : 1.0;

                // Extract dominant color (optional, slightly slower)
                string? dominantColor = ExtractDominantColor(filePath);

                return new MetadataResult
                {
                    AssetId = assetId,
                    Width = width,
                    Height = height,
                    AspectRatio = aspectRatio,
                    DominantColor = dominantColor
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning("MetadataIndexingService: Failed to extract metadata for asset {Id}: {Error}", 
                    assetId, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Extract dominant color by resizing image to tiny thumbnail and calculating average.
        /// </summary>
        private string? ExtractDominantColor(string filePath)
        {
            try
            {
                using var image = Image.Load<Rgba32>(filePath);
                
                // Resize to tiny size for fast average calculation
                image.Mutate(x => x.Resize(ThumbnailSizeForColorExtraction, ThumbnailSizeForColorExtraction));

                long totalR = 0, totalG = 0, totalB = 0;
                int pixelCount = 0;

                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x++)
                        {
                            var pixel = row[x];
                            totalR += pixel.R;
                            totalG += pixel.G;
                            totalB += pixel.B;
                            pixelCount++;
                        }
                    }
                });

                if (pixelCount == 0) return null;

                byte avgR = (byte)(totalR / pixelCount);
                byte avgG = (byte)(totalG / pixelCount);
                byte avgB = (byte)(totalB / pixelCount);

                return $"#{avgR:X2}{avgG:X2}{avgB:X2}";
            }
            catch
            {
                return null; // Color extraction is optional
            }
        }

        /// <summary>
        /// Internal result class for metadata extraction.
        /// </summary>
        private class MetadataResult
        {
            public int AssetId { get; init; }
            public int Width { get; init; }
            public int Height { get; init; }
            public double AspectRatio { get; init; }
            public string? DominantColor { get; init; }
        }
    }
}
