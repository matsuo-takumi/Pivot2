using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pivot.Engine;

namespace Pivot.Services
{
	public interface IThumbnailService : IDisposable
	{
		Task InitializeAsync(string cacheDirectory, long maxCacheBytes);
		Task<string> GetOrCreateThumbnailAsync(string sourcePath, int width, int height, CancellationToken ct = default);
		string? TryGetCachedThumbnailPath(string sourcePath, int width, int height);
	}

	/// <summary>
	/// Lightweight wrapper around PivotEngine for thumbnail generation.
	/// All heavy processing (ImageSharp, FFMPEG, Shell API) is delegated to the Engine layer.
	/// </summary>
	public sealed class ThumbnailService : IThumbnailService
	{
		private readonly ILogger<ThumbnailService> _logger;
		private readonly IPivotEngine _pivotEngine;

		public ThumbnailService(ILogger<ThumbnailService> logger, IPivotEngine pivotEngine)
		{
			_logger = logger;
			_pivotEngine = pivotEngine;
		}

		public Task InitializeAsync(string cacheDirectory, long maxCacheBytes)
		{
			// Engine initialization is handled in App.xaml.cs OnLaunched
			// This method is kept for backward compatibility with existing UI code
			_logger.LogInformation("ThumbnailService initialized (delegating to PivotEngine)");
			return Task.CompletedTask;
		}

		public async Task<string> GetOrCreateThumbnailAsync(string sourcePath, int width, int height, CancellationToken ct = default)
		{
			try
			{
				// Delegate all thumbnail generation to the Engine layer
				// Engine handles caching, job scheduling, and background processing
				var thumbnailPath = await _pivotEngine.GetThumbnailAsync(sourcePath, width, height, ct);
				
				// If Engine returns empty string, it means the job is queued but not yet complete
				// UI should handle this by showing a placeholder or the original image
				if (string.IsNullOrEmpty(thumbnailPath))
				{
					// _logger.LogDebug("Thumbnail generation in progress for {Path}", sourcePath);
					return string.Empty;
				}

				return thumbnailPath;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to get thumbnail from engine for {Path}", sourcePath);
				// Return empty string on error - UI will fall back to placeholder or original image
				return string.Empty;
			}
		}

		public string? TryGetCachedThumbnailPath(string sourcePath, int width, int height)
		{
			// Synchronous cache check - for now, return null to force async loading
			return null; 
		}

		public void Dispose()
		{
			// Engine lifecycle is managed by DI container (singleton)
		}
	}
}


