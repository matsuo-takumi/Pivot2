using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pivot.Models;
using Pivot.Services.ImageEngine;

namespace Pivot.Services.Engines
{
    /// <summary>
    /// Engine for handling image assets.
    /// Responsible for thumbnail generation and metadata extraction for image files.
    /// </summary>
    public class ImageEngine : IAssetEngine
    {
        private readonly IThumbnailService _thumbnailService;
        private readonly ILogger<ImageEngine> _logger;

        public string[] SupportedExtensions => new[] 
        { 
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tga", ".tif", ".tiff", ".webp" 
        };

        public int Priority => 10;

        public ImageEngine(
            IThumbnailService thumbnailService,
            ILogger<ImageEngine> logger)
        {
            _thumbnailService = thumbnailService;
            _logger = logger;
        }

        public async Task ProcessFileAsync(string filePath, AssetEntity asset, CancellationToken ct = default)
        {
            try
            {
                // 1. Generate Thumbnail
                // If thumbnail is missing or marked for regeneration, generate it.
                // Note: FileScannerService might have already checked existence, but checking again is safe.
                if (string.IsNullOrEmpty(asset.ThumbnailPath) || !_thumbnailService.ThumbnailExists(asset.Hash))
                {
                    var thumbnailPath = await _thumbnailService.GenerateThumbnailAsync(filePath, asset.Hash, ct: ct);
                    
                    if (!string.IsNullOrEmpty(thumbnailPath))
                    {
                        asset.ThumbnailPath = thumbnailPath;
                        asset.ThumbnailGeneratedAt = DateTime.UtcNow;
                    }
                }

                // 2. Extract Metadata (Dimensions)
                try
                {
                    var imageInfo = await SixLabors.ImageSharp.Image.IdentifyAsync(filePath, ct);
                    if (imageInfo != null)
                    {
                        asset.Width = imageInfo.Width;
                        asset.Height = imageInfo.Height;
                        asset.AspectRatio = imageInfo.Height > 0 ? (double)imageInfo.Width / imageInfo.Height : 0;
                    }
                }
                catch (Exception dimEx)
                {
                    _logger.LogDebug(dimEx, "Failed to read image dimensions: {FilePath}", filePath);
                }

                // 3. Dominant Color (Optional/Future)
                // asset.DominantColor = await _colorExtractor.GetDominantColorAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image file: {FilePath}", filePath);
                // Don't rethrow, just log. Partial processing is better than crash.
            }
        }

        public bool IsUpToDate(AssetEntity asset)
        {
            if (string.IsNullOrEmpty(asset.ThumbnailPath)) return false;
            return _thumbnailService.ThumbnailExists(asset.Hash ?? "");
        }
    }
}
