using System;
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
					_logger.LogDebug("Thumbnail generation in progress for {Path}", sourcePath);
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
			// In the future, Engine could expose a synchronous cache lookup method
			// For now, UI should always use GetOrCreateThumbnailAsync
			return null;
		}

		public void Dispose()
		{
			// Engine lifecycle is managed by DI container (singleton)
			// No cleanup needed here
		}
	}
}


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
		private readonly SemaphoreSlim _parallelismSemaphore;
		private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<string>> _creationTasks = new(System.StringComparer.OrdinalIgnoreCase);
		private long _lastCleanupTicks = 0;
		private const long CleanupIntervalMs = 5000;
		private string _cacheDir = string.Empty;
		private long _maxCacheBytes = 500L * 1024 * 1024; // 500MB default
		private DispatcherQueue? _uiDispatcher;
		private ShellIconService? _shellIconService;

		public ThumbnailService(ILogger<ThumbnailService> logger)
		{
			_logger = logger;
			_parallelismSemaphore = new SemaphoreSlim(Math.Max(2, Environment.ProcessorCount / 2));
		}

		public Task InitializeAsync(string cacheDirectory, long maxCacheBytes)
		{
			_cacheDir = cacheDirectory;
			_maxCacheBytes = maxCacheBytes > 0 ? maxCacheBytes : _maxCacheBytes;
			Directory.CreateDirectory(_cacheDir);
			// capture UI dispatcher when initialized from UI thread so we can render XAML visuals
			try { _uiDispatcher = DispatcherQueue.GetForCurrentThread(); } catch { }
			// init shell icon helper (icons cached under cache/icons)
			try { _shellIconService = new ShellIconService(Path.Combine(_cacheDir, "icons")); } catch { }
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

			if (File.Exists(thumbPath)) return thumbPath;

			var creationTask = _creationTasks.GetOrAdd(key, (_) => Task.Run(async () =>
			{
				await _parallelismSemaphore.WaitAsync(ct).ConfigureAwait(false);
				try
				{
					if (File.Exists(thumbPath)) return thumbPath;
					await CreateThumbnailAsync(sourcePath, width, height, thumbPath, ct).ConfigureAwait(false);
					var now = Environment.TickCount64;
					if (now - Interlocked.Read(ref _lastCleanupTicks) >= CleanupIntervalMs)
					{
						Interlocked.Exchange(ref _lastCleanupTicks, now);
						// キャッシュクリーンアップをバックグラウンドで実行（結果を待たない）
						var cleanupTask = Task.Run(() => TryCleanupCache());
						// タスクの完了を待たない（fire-and-forget）
					}
					return thumbPath;
				}
				finally { _parallelismSemaphore.Release(); }
			}));

			try { return await creationTask.ConfigureAwait(false); }
			finally { _creationTasks.TryRemove(key, out _); }
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
					// 大きな画像の最適化: ImageSharpはメモリ効率的に動作するが、
					// リサイズオプションを最適化してパフォーマンスを向上
					using var image = await ImageSharpImage.LoadAsync(sourcePath, ct).ConfigureAwait(false);
					
					// リサイズオプションを最適化: 大きな画像の場合は効率的なリサンプラーを使用
					var resizeOptions = new ResizeOptions
					{
						Mode = ResizeMode.Max,
						Size = new SixLabors.ImageSharp.Size(width, height),
						// 大きな画像（4K以上）の場合はLanczos3、それ以下はBicubicを使用
						// Lanczos3は高品質だが処理が重いため、大きな画像にのみ適用
						Sampler = (image.Width > 2000 || image.Height > 2000) 
							? KnownResamplers.Lanczos3 
							: KnownResamplers.Bicubic,
						// メモリ効率を優先（ガンマ補正を無効化）
						Compand = false
					};
					
					image.Mutate(x => x.Resize(resizeOptions));
					Directory.CreateDirectory(Path.GetDirectoryName(destinationPngPath)!);
					
					// PNGエンコーダーオプションで圧縮率を最適化（ファイルサイズ削減）
					var encoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder
					{
						CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.BestCompression
					};
					
					await image.SaveAsPngAsync(destinationPngPath, encoder, ct).ConfigureAwait(false);
					return;
				}

                var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
                var videoExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase){ ".mp4", ".mov", ".avi", ".mkv", ".webm" };

                // try video frame extraction first for known video formats (requires ffmpeg in PATH)
                if (videoExts.Contains(ext))
                {
                    try
                    {
                        if (await TryExtractVideoFrameWithFfmpegAsync(sourcePath, width, height, destinationPngPath, ct).ConfigureAwait(false))
                        {
                            return;
                        }
                    }
                    catch { }
                    // fallthrough to other handlers if ffmpeg not available or failed
                }

                var modelExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase){ ".obj", ".fbx", ".gltf", ".glb", ".dae", ".3ds", ".max", ".blend", ".ma", ".mb", ".usd", ".usdz", ".ply", ".stl" };
                if (modelExts.Contains(ext))
                {
                    // Try Windows Shell API for actual file thumbnail first (better quality)
                    try
                    {
                        if (TryCreateThumbnailFromShell(sourcePath, width, height, destinationPngPath))
                        {
                            return;
                        }
                    }
                    catch { }
                    
                    // Fallback to shell icon service (per-extension cache)
                    try
                    {
                        if (_shellIconService != null)
                        {
                            var iconPath = await _shellIconService.GetOrCreateIconForExtensionAsync(ext, Math.Max(width, height), ct).ConfigureAwait(false);
                            if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
                            {
                                // copy cached icon to destination (resize not necessary; we generate at requested size)
                                Directory.CreateDirectory(Path.GetDirectoryName(destinationPngPath)!);
                                File.Copy(iconPath, destinationPngPath, true);
                                return;
                            }
                        }
                    }
                    catch { }

					var label = ext.StartsWith(".") ? ext.Substring(1).ToUpperInvariant() : ext.ToUpperInvariant();
					if (_uiDispatcher != null)
					{
						try { await RenderLabelWithWinUIAsync(label, width, height, destinationPngPath, ct).ConfigureAwait(false); return; } catch { }
					}

					// fallback magick draw
					// try System.Drawing text fallback first (guaranteed on Windows)
					try
					{
						using var bmp = new System.Drawing.Bitmap(Math.Max(1, width), Math.Max(1, height));
						using var g = System.Drawing.Graphics.FromImage(bmp);
						// background
						g.Clear(System.Drawing.Color.FromArgb(0x2D, 0x6C, 0xDF));
						// draw a white rounded-ish box
                        using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                        {
                            int bx0 = (int)(width * 0.18f);
                            int by0 = (int)(height * 0.18f);
                            int brw = (int)(width * 0.64f);
                            int brh = (int)(height * 0.54f);
                            g.FillRectangle(brush, bx0, by0, brw, brh);
                        }
						// label text
						var labelText = label;
						using var font = new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, Math.Max(10, width / 10), System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
						var sf = new System.Drawing.StringFormat() { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
						using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);
						var rect = new System.Drawing.Rectangle(0, height - (int)(font.Size * 2) - 6, width, (int)(font.Size * 2) + 6);
						g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
						g.DrawString(labelText, font, textBrush, rect, sf);
						Directory.CreateDirectory(Path.GetDirectoryName(destinationPngPath)!);
						bmp.Save(destinationPngPath, System.Drawing.Imaging.ImageFormat.Png);
						return;
					}
					catch { }

					using var mag = new MagickImage(new MagickColor("#2D6CDF"), width, height);
					int x0 = (int)(width * 0.18f);
					int y0 = (int)(height * 0.18f);
					int rw = (int)(width * 0.64f);
					int rh = (int)(height * 0.54f);
					var drawBox = new Drawables().FillColor(MagickColors.White).Rectangle(x0, y0, x0 + rw, y0 + rh);
					drawBox.Draw(mag);
					var fontSize = Math.Max(10, width / 10);
					var drawLabel = new Drawables().FontPointSize(fontSize).FillColor(MagickColors.Black).TextAlignment(ImageMagick.TextAlignment.Center).Text(width / 2, height - (int)(fontSize * 0.5) - 6, label);
					drawLabel.Draw(mag);
					mag.Format = MagickFormat.Png;
					Directory.CreateDirectory(Path.GetDirectoryName(destinationPngPath)!);
					await mag.WriteAsync(destinationPngPath, ct).ConfigureAwait(false);
					return;
				}
				else
				{
					using var mag = new MagickImage(sourcePath);
					mag.Resize(width, height);
					mag.Format = MagickFormat.Png;
					Directory.CreateDirectory(Path.GetDirectoryName(destinationPngPath)!);
					await mag.WriteAsync(destinationPngPath, ct).ConfigureAwait(false);
					return;
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Thumbnail generation failed for {Path}. Writing placeholder.", sourcePath);
				using var img = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
				Directory.CreateDirectory(Path.GetDirectoryName(destinationPngPath)!);
				await img.SaveAsPngAsync(destinationPngPath, ct).ConfigureAwait(false);
			}
		}

		private Task RenderLabelWithWinUIAsync(string labelText, int width, int height, string destinationPngPath, CancellationToken ct)
		{
			var tcs = new TaskCompletionSource<bool>();
			_uiDispatcher!.TryEnqueue(async () =>
			{
				try
				{
					var grid = new Grid() { Width = (double)width, Height = (double)height, Background = new SolidColorBrush(Microsoft.UI.Colors.Black) };
					var tb = new TextBlock()
					{
						Text = labelText,
						HorizontalAlignment = HorizontalAlignment.Center,
						VerticalAlignment = VerticalAlignment.Center,
						Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
						FontSize = Math.Max(12, width / 10)
					};
					grid.Children.Add(tb);

					var rtb = new RenderTargetBitmap();
					await rtb.RenderAsync(grid, width, height);
					var pixels = await rtb.GetPixelsAsync();

					// read bytes from buffer
					var readerBuf = DataReader.FromBuffer(pixels);
					var bytes = new byte[readerBuf.UnconsumedBufferLength];
					readerBuf.ReadBytes(bytes);

					using var mem = new InMemoryRandomAccessStream();
					var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, mem);
					encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)width, (uint)height, 96, 96, bytes);
					await encoder.FlushAsync();
					mem.Seek(0);

					using var outFs = new FileStream(destinationPngPath, FileMode.Create, FileAccess.Write);
					var reader = new DataReader(mem.GetInputStreamAt(0));
					await reader.LoadAsync((uint)mem.Size);
					var buffer = new byte[mem.Size];
					reader.ReadBytes(buffer);
					await outFs.WriteAsync(buffer, 0, buffer.Length);

					tcs.SetResult(true);
				}
				catch (Exception ex) { tcs.SetException(ex); }
			});

			return tcs.Task;
		}

		private async Task<bool> TryExtractVideoFrameWithFfmpegAsync(string sourcePath, int width, int height, string destinationPngPath, CancellationToken ct)
		{
			try
			{
				// ffmpeg should be on PATH. We seek 1s into the video to avoid black frames at start.
				var ffmpeg = "ffmpeg";
				var args = $"-y -ss 00:00:01 -i \"{sourcePath}\" -vframes 1 -vf \"scale='min({width},iw)':'min({height},ih)':force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2\" \"{destinationPngPath}\"";

				var psi = new ProcessStartInfo(ffmpeg, args)
				{
					CreateNoWindow = true,
					UseShellExecute = false,
					RedirectStandardError = true,
					RedirectStandardOutput = true,
				};

				using var proc = Process.Start(psi);
				if (proc == null) return false;

				using (ct.Register(() =>
				{
					try { if (!proc.HasExited) proc.Kill(true); } catch { }
				}))
				{
					await proc.WaitForExitAsync(ct).ConfigureAwait(false);
				}

				return proc.ExitCode == 0 && File.Exists(destinationPngPath);
			}
			catch
			{
				return false;
			}
		}

		private static bool CanLoadWithImageSharp(string sourcePath)
		{
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

		private bool TryCreateThumbnailFromShell(string sourcePath, int width, int height, string destinationPngPath)
		{
			try
			{
				// Use IShellItemImageFactory to get actual file thumbnail (same as Explorer)
				Guid shellItemGuid = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"); // IShellItem
				Guid imageFactoryGuid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"); // IShellItemImageFactory
				
				IntPtr shellItemPtr = IntPtr.Zero;
				IntPtr imageFactoryPtr = IntPtr.Zero;
				IntPtr hBitmap = IntPtr.Zero;

				try
				{
					// Create IShellItem from file path
					int hr = SHCreateItemFromParsingName(sourcePath, IntPtr.Zero, shellItemGuid, out shellItemPtr);
					if (hr != 0 || shellItemPtr == IntPtr.Zero)
					{
						return false;
					}

					// Query IShellItemImageFactory interface
					hr = Marshal.QueryInterface(shellItemPtr, ref imageFactoryGuid, out imageFactoryPtr);
					if (hr != 0 || imageFactoryPtr == IntPtr.Zero)
					{
						return false;
					}

					// Get thumbnail bitmap using IShellItemImageFactory.GetImage
					var size = new SIZE { cx = width, cy = height };
					uint flags = 0x00000001; // SIIGBF_THUMBNAILONLY
					hr = IShellItemImageFactory_GetImage(imageFactoryPtr, ref size, flags, out hBitmap);
					if (hr != 0 || hBitmap == IntPtr.Zero)
					{
						return false;
					}

					// Convert HBITMAP to PNG file
					using var bmp = System.Drawing.Image.FromHbitmap(hBitmap);
					using var resized = new System.Drawing.Bitmap(bmp, Math.Max(1, width), Math.Max(1, height));
					Directory.CreateDirectory(Path.GetDirectoryName(destinationPngPath)!);
					resized.Save(destinationPngPath, System.Drawing.Imaging.ImageFormat.Png);
					return File.Exists(destinationPngPath);
				}
				finally
				{
					if (hBitmap != IntPtr.Zero)
					{
						DeleteObject(hBitmap);
					}
					if (imageFactoryPtr != IntPtr.Zero)
					{
						Marshal.Release(imageFactoryPtr);
					}
					if (shellItemPtr != IntPtr.Zero)
					{
						Marshal.Release(shellItemPtr);
					}
				}
			}
			catch { }
			return false;
		}

		#region Native Shell API
		[StructLayout(LayoutKind.Sequential)]
		private struct SIZE
		{
			public int cx;
			public int cy;
		}

		[DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
		private static extern int SHCreateItemFromParsingName(
			[MarshalAs(UnmanagedType.LPWStr)] string pszPath,
			IntPtr pbc,
			[MarshalAs(UnmanagedType.LPStruct)] Guid riid,
			out IntPtr ppv);

		[DllImport("gdi32.dll")]
		private static extern bool DeleteObject(IntPtr hObject);

		// IShellItemImageFactory.GetImage method (vtable offset 3)
		private static int IShellItemImageFactory_GetImage(IntPtr pImageFactory, ref SIZE size, uint flags, out IntPtr phbm)
		{
			phbm = IntPtr.Zero;
			try
			{
				// Get vtable pointer
				IntPtr vtable = Marshal.ReadIntPtr(pImageFactory);
				// Get GetImage method pointer (offset 3 * IntPtr.Size)
				IntPtr getImagePtr = Marshal.ReadIntPtr(vtable, 3 * IntPtr.Size);
				
				// Create delegate for GetImage method
				var getImageDelegate = Marshal.GetDelegateForFunctionPointer<GetImageDelegate>(getImagePtr);
				return getImageDelegate(pImageFactory, ref size, flags, out phbm);
			}
			catch
			{
				return -1;
			}
		}

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate int GetImageDelegate(IntPtr pImageFactory, ref SIZE size, uint flags, out IntPtr phbm);
		#endregion

		public void Dispose()
		{
			try { _parallelismSemaphore?.Dispose(); } catch { }
			try { _shellIconService?.Dispose(); } catch { }
		}
	}
}


