using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;
using ImageMagick;

namespace Pivot.Services
{
	public interface IThumbnailService : IDisposable
	{
		Task InitializeAsync(string cacheDirectory, long maxCacheBytes);
		Task<string> GetOrCreateThumbnailAsync(string sourcePath, int width, int height, CancellationToken ct = default);
	}

	public sealed class ThumbnailService : IThumbnailService
	{
		private readonly ILogger<ThumbnailService> _logger;
		private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
		private string _cacheDir = string.Empty;
		private long _maxCacheBytes = 500L * 1024 * 1024; // 500MB default

		public ThumbnailService(ILogger<ThumbnailService> logger)
		{
			_logger = logger;
		}

		public Task InitializeAsync(string cacheDirectory, long maxCacheBytes)
		{
			_cacheDir = cacheDirectory;
			_maxCacheBytes = maxCacheBytes > 0 ? maxCacheBytes : _maxCacheBytes;
			Directory.CreateDirectory(_cacheDir);
			return Task.CompletedTask;
		}

		public async Task<string> GetOrCreateThumbnailAsync(string sourcePath, int width, int height, CancellationToken ct = default)
		{
			ct.ThrowIfCancellationRequested();
			if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
				throw new FileNotFoundException("Source image not found", sourcePath);

			var info = new FileInfo(sourcePath);
			var key = ComputeKey(sourcePath, info.LastWriteTimeUtc.Ticks, info.Length, width, height);
			var thumbPath = Path.Combine(_cacheDir, key + ".png");

			if (File.Exists(thumbPath))
			{
				return thumbPath;
			}

			await _writeLock.WaitAsync(ct).ConfigureAwait(false);
			try
			{
				if (File.Exists(thumbPath)) return thumbPath; // recheck under lock

				await CreateThumbnailAsync(sourcePath, width, height, thumbPath, ct).ConfigureAwait(false);
				_ = Task.Run(() => TryCleanupCache(), CancellationToken.None);
				return thumbPath;
			}
			finally
			{
				_writeLock.Release();
			}
		}

		private static string ComputeKey(string path, long ticks, long size, int w, int h)
		{
			using var sha = SHA256.Create();
			var raw = Encoding.UTF8.GetBytes(path + "|" + ticks + "|" + size + "|" + w + "x" + h);
			var hash = sha.ComputeHash(raw);
			var sb = new StringBuilder(hash.Length * 2);
			foreach (var b in hash) sb.Append(b.ToString("x2"));
			return sb.ToString();
		}

		private static void RegisterImageSharpFormats(Configuration cfg)
		{
			cfg.ImageFormatsManager.SetEncoder(PngFormat.Instance, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
		}

		private async Task CreateThumbnailAsync(string sourcePath, int width, int height, string destinationPngPath, CancellationToken ct)
		{
			try
			{
				if (CanLoadWithImageSharp(sourcePath))
				{
					var cfg = Configuration.Default.Clone();
					RegisterImageSharpFormats(cfg);
					using var image = await Image.LoadAsync(cfg, sourcePath, ct).ConfigureAwait(false);
					image.Mutate(x => x.Resize(new ResizeOptions
					{
						Mode = ResizeMode.Max,
						Size = new Size(width, height)
					}));
					Directory.CreateDirectory(Path.GetDirectoryName(destinationPngPath)!);
					await image.SaveAsPngAsync(destinationPngPath, ct).ConfigureAwait(false);
				}
				else
				{
					using var img = new MagickImage(sourcePath);
					img.Resize(width, height);
					img.Format = MagickFormat.Png;
					Directory.CreateDirectory(Path.GetDirectoryName(destinationPngPath)!);
					await img.WriteAsync(destinationPngPath, ct).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Thumbnail generation failed for {Path}. Writing placeholder.", sourcePath);
				// Write a tiny transparent PNG placeholder
				using var img = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
				Directory.CreateDirectory(Path.GetDirectoryName(destinationPngPath)!);
				await img.SaveAsPngAsync(destinationPngPath, ct).ConfigureAwait(false);
			}
		}

		private static bool CanLoadWithImageSharp(string sourcePath)
		{
			// ImageSharp covers most common formats; for ico/tiff/tga we fallback to Magick.NET
			var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
			switch (ext)
			{
				case ".ico":
				case ".tif":
				case ".tiff":
				case ".tga":
					return false;
				default:
					return true;
			}
		}

		private void TryCleanupCache()
		{
			try
			{
				var dir = new DirectoryInfo(_cacheDir);
				if (!dir.Exists) return;
				var files = dir.GetFiles("*.png", SearchOption.TopDirectoryOnly);
				long total = files.Sum(f => f.Length);
				if (total <= _maxCacheBytes) return;
				foreach (var f in files.OrderBy(f => f.LastAccessTimeUtc))
				{
					try { f.Delete(); } catch { }
					total -= f.Length;
					if (total <= _maxCacheBytes) break;
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Thumbnail cache cleanup failed");
			}
		}

		public void Dispose()
		{
			try { _writeLock.Dispose(); } catch { }
		}
	}
}


