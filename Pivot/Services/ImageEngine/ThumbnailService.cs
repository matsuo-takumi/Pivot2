using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Pivot.Services.ImageEngine
{
    /// <summary>
    /// Interface for thumbnail generation service.
    /// </summary>
    public interface IThumbnailService
    {
        /// <summary>
        /// Generate a thumbnail for the given source image.
        /// </summary>
        /// <param name="sourcePath">Path to the original image file</param>
        /// <param name="hash">Hash of the file (used for thumbnail filename)</param>
        /// <param name="maxSize">Maximum size of the longest edge (default: 256px)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Path to the generated thumbnail, or null if generation failed</returns>
        Task<string?> GenerateThumbnailAsync(
            string sourcePath, 
            string hash, 
            int maxSize = 256, 
            CancellationToken ct = default);
        
        /// <summary>
        /// Get the expected thumbnail path for a given hash.
        /// </summary>
        string GetThumbnailPath(string hash);
        
        /// <summary>
        /// Check if a thumbnail exists for the given hash.
        /// </summary>
        bool ThumbnailExists(string hash);
    }

    /// <summary>
    /// Service for generating and managing disk-based thumbnail cache (L2 Cache).
    /// Generates 256px JPEG thumbnails with quality 80 to reduce I/O load.
    /// </summary>
    public class ThumbnailService : IThumbnailService
    {
        private readonly ILogger<ThumbnailService> _logger;
        private readonly string _cacheDirectory;
        private const int DefaultQuality = 80;

        public ThumbnailService(ILogger<ThumbnailService> logger)
        {
            _logger = logger;
            
            // Cache directory: %LOCALAPPDATA%\Pivot\.pivot\cache\thumbnails
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _cacheDirectory = Path.Combine(localAppData, "Pivot", ".pivot", "cache", "thumbnails");
            
            // Ensure cache directory exists
            Directory.CreateDirectory(_cacheDirectory);
        }

        public string GetThumbnailPath(string hash)
        {
            if (string.IsNullOrEmpty(hash))
                throw new ArgumentException("Hash cannot be null or empty", nameof(hash));
            
            return Path.Combine(_cacheDirectory, $"{hash}.jpg");
        }

        public bool ThumbnailExists(string hash)
        {
            var thumbnailPath = GetThumbnailPath(hash);
            return File.Exists(thumbnailPath);
        }

        public async Task<string?> GenerateThumbnailAsync(
            string sourcePath, 
            string hash, 
            int maxSize = 256, 
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                _logger.LogWarning("GenerateThumbnailAsync: sourcePath is null or empty");
                return null;
            }

            if (string.IsNullOrEmpty(hash))
            {
                _logger.LogWarning("GenerateThumbnailAsync: hash is null or empty for {SourcePath}", sourcePath);
                return null;
            }

            if (!File.Exists(sourcePath))
            {
                _logger.LogWarning("GenerateThumbnailAsync: Source file not found: {SourcePath}", sourcePath);
                return null;
            }

            var thumbnailPath = GetThumbnailPath(hash);

            // Skip if thumbnail already exists
            if (File.Exists(thumbnailPath))
            {
                _logger.LogDebug("Thumbnail already exists: {ThumbnailPath}", thumbnailPath);
                return thumbnailPath;
            }

            try
            {
                // Load image from source file
                using var image = await Image.LoadAsync(sourcePath, ct);
                
                // Calculate new dimensions while maintaining aspect ratio
                int newWidth, newHeight;
                if (image.Width > image.Height)
                {
                    // Landscape: limit width
                    newWidth = Math.Min(image.Width, maxSize);
                    newHeight = (int)((double)image.Height / image.Width * newWidth);
                }
                else
                {
                    // Portrait or square: limit height
                    newHeight = Math.Min(image.Height, maxSize);
                    newWidth = (int)((double)image.Width / image.Height * newHeight);
                }

                // Resize image
                image.Mutate(x => x.Resize(newWidth, newHeight));

                // Save as JPEG with quality 80
                var encoder = new JpegEncoder
                {
                    Quality = DefaultQuality
                };

                await image.SaveAsync(thumbnailPath, encoder, ct);

                _logger.LogDebug("Generated thumbnail: {ThumbnailPath} ({Width}x{Height})", 
                    thumbnailPath, newWidth, newHeight);

                return thumbnailPath;
            }
            catch (UnknownImageFormatException ex)
            {
                _logger.LogWarning(ex, "Unsupported image format: {SourcePath}", sourcePath);
                return null;
            }
            catch (InvalidImageContentException ex)
            {
                _logger.LogWarning(ex, "Corrupted or invalid image: {SourcePath}", sourcePath);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate thumbnail for {SourcePath}", sourcePath);
                return null;
            }
        }
    }
}
