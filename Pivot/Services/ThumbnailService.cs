using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
		string? TryGetCachedThumbnailPath(string sourcePath, int width, int height);
	}

	public sealed class ThumbnailService : IThumbnailService
	{
		private readonly ILogger<ThumbnailService> _logger;
		// 限定並列でサムネイル生成を行うためのセマフォ
		private readonly SemaphoreSlim _parallelismSemaphore;
		// 同一キーに対する重複生成を抑止するための作成タスク辞書
		private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<string>> _creationTasks = new System.Collections.Concurrent.ConcurrentDictionary<string, Task<string>>();
		// クリーンアップの呼び出し頻度を抑制（過負荷防止）
		private long _lastCleanupTicks = 0;
		private const long CleanupIntervalMs = 5000;
		private string _cacheDir = string.Empty;
		private long _maxCacheBytes = 500L * 1024 * 1024; // 500MB default

		public ThumbnailService(ILogger<ThumbnailService> logger)
		{
			_logger = logger;
			// 並列度は環境に依存して調整（最小2）
			_parallelismSemaphore = new SemaphoreSlim(Math.Max(2, Environment.ProcessorCount / 2));
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

			// 既に作成中のタスクがあればそれを待つ（重複作成を抑止）
			var creationTask = _creationTasks.GetOrAdd(key, (_) => Task.Run(async () =>
			{
				// 制限付き並列で実際の生成を実行
				await _parallelismSemaphore.WaitAsync(ct).ConfigureAwait(false);
				try
				{
					if (File.Exists(thumbPath)) return thumbPath; // 再確認
					await CreateThumbnailAsync(sourcePath, width, height, thumbPath, ct).ConfigureAwait(false);
					var now = Environment.TickCount64;
					if (now - Interlocked.Read(ref _lastCleanupTicks) >= CleanupIntervalMs)
					{
						Interlocked.Exchange(ref _lastCleanupTicks, now);
						Task.Run(() => TryCleanupCache());
					}
					return thumbPath;
				}
				finally
				{
					_parallelismSemaphore.Release();
				}
			}));

			try
			{
				return await creationTask.ConfigureAwait(false);
			}
			finally
			{
				// 完了したタスクは辞書から削除してメモリ増加を抑える
				_creationTasks.TryRemove(key, out _);
			}
		}

		public string? TryGetCachedThumbnailPath(string sourcePath, int width, int height)
		{
			if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return null;
			var info = new FileInfo(sourcePath);
			var key = ComputeKey(sourcePath, info.LastWriteTimeUtc.Ticks, info.Length, width, height);
			var thumbPath = Path.Combine(_cacheDir, key + ".png");
			return File.Exists(thumbPath) ? thumbPath : null;
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
			cfg.ImageFormatsManager.SetEncoder(SixLabors.ImageSharp.Formats.Png.PngFormat.Instance, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
		}

		private async Task CreateThumbnailAsync(string sourcePath, int width, int height, string destinationPngPath, CancellationToken ct)
		{
			try
			{
				if (CanLoadWithImageSharp(sourcePath))
				{
					using var image = await Image.LoadAsync(sourcePath, ct).ConfigureAwait(false);
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
				// For non-image sources (e.g. some vector formats) try Magick.NET; for 3D model files produce a simple placeholder
				var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
				var modelExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase){ ".obj", ".fbx", ".gltf", ".glb", ".dae" };
				if (modelExts.Contains(ext))
				{
					// create a simple placeholder PNG for 3D models (colored background with white box)
					using var img = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
					var bgColor = new SixLabors.ImageSharp.PixelFormats.Rgba32(0x2D, 0x6C, 0xDF, 0xFF); // #2D6CDF
					var fgColor = new SixLabors.ImageSharp.PixelFormats.Rgba32(0xFF, 0xFF, 0xFF, 0xFF);
					img.ProcessPixelRows(accessor =>
					{
						// fill background
						for (int y = 0; y < height; y++)
						{
							var row = accessor.GetRowSpan(y);
							for (int x = 0; x < width; x++) row[x] = bgColor;
						}

						// outer rect
						int x0 = (int)(width * 0.18f);
						int y0 = (int)(height * 0.28f);
						int rw = (int)(width * 0.64f);
						int rh = (int)(height * 0.44f);
						for (int y = y0; y < Math.Min(height, y0 + rh); y++)
						{
							var row = accessor.GetRowSpan(y);
							for (int x = x0; x < Math.Min(width, x0 + rw); x++) row[x] = fgColor;
						}

						// inner rect
						int ix = (int)(width * 0.28f);
						int iy = (int)(height * 0.38f);
						int iw = (int)(width * 0.44f);
						int ih = (int)(height * 0.24f);
						for (int y = iy; y < Math.Min(height, iy + ih); y++)
						{
							var row = accessor.GetRowSpan(y);
							for (int x = ix; x < Math.Min(width, ix + iw); x++) row[x] = bgColor;
						}
					});
					Directory.CreateDirectory(Path.GetDirectoryName(destinationPngPath)!);
					await img.SaveAsPngAsync(destinationPngPath, ct).ConfigureAwait(false);
				}
				else
				{
					using var mag = new MagickImage(sourcePath);
					mag.Resize(width, height);
					mag.Format = MagickFormat.Png;
					Directory.CreateDirectory(Path.GetDirectoryName(destinationPngPath)!);
					await mag.WriteAsync(destinationPngPath, ct).ConfigureAwait(false);
				}
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
			try { _parallelismSemaphore?.Dispose(); } catch { }
		}
	}
}


