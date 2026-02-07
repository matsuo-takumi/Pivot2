using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;
using System;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Models;
using Pivot.Messages;
using System.Timers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;

namespace Pivot.Services
{
	public class FileScannerService : IDisposable
	{
		private readonly ILogger<FileScannerService> _logger;
		private readonly IServiceProvider _serviceProvider;  // For scoped DB access
		private readonly Pivot.Repositories.IAssetRepository _repository;  // Fallback for non-parallel ops
		private readonly Pivot.Services.ImageEngine.IThumbnailService? _thumbnailService;
		private readonly IMessenger _messenger;
		private string? _currentRootPath;
		// Phase 4: 複数ルート監視に備えたコレクション（段階的導入）
		private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
		private IReadOnlyCollection<string> _roots = Array.Empty<string>();
		private CancellationTokenSource? _watcherCts;
		private Task? _watcherProcessingTask;
		private readonly ConcurrentDictionary<string, (WatcherChangeTypes ChangeType, string? NewPath, DateTime EnqueuedAt)> _pendingEvents = new ConcurrentDictionary<string, (WatcherChangeTypes, string?, DateTime)>();

		// 短期間の複数イベントをまとめるためのディレイとキュー
		private readonly ConcurrentDictionary<string, CancellationTokenSource> _delayTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
		private const int EventDelayMs = 500;

		private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga", ".tif", ".tiff", ".webp"
		};

		// Asset extensions include images and 3D model formats
		private static readonly HashSet<string> AssetExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga", ".tif", ".tiff", ".webp",
			".fbx", ".obj", ".usd", ".usdz", ".gltf", ".glb", ".hdr", ".exr"
		};

		private static bool IsAssetExtension(string ext)
		{
			if (string.IsNullOrEmpty(ext)) return false;
			return AssetExtensions.Contains(ext);
		}

		// Default settings (can be tuned or moved to configuration)
		private const bool UseQuickCheck = true;
		private const int DefaultMaxOpenFiles = 64;
		private const int DefaultRetryCount = 3;
		private const int DefaultRetryDelayMs = 200;

		private SemaphoreSlim? _fileOpenSemaphore;
		private SemaphoreSlim? _hashSemaphore;

		// ===== Phase 2: Buffers & periodic flush =====
		private readonly ConcurrentQueue<ItemChangeData<AssetEntity>> _assetChangeBuffer = new();
		private CancellationTokenSource? _flushLoopCts;
		private Task? _flushLoopTask;
		private const int AssetFlushIntervalMs = 300; // Phase 6: move to config
		private const int AssetFlushBatchMax = 100;   // Phase 6: move to config

		        public FileScannerService(
            ILogger<FileScannerService> logger, 
            IServiceProvider serviceProvider,
            Pivot.Repositories.IAssetRepository repository, 
            IMessenger messenger, 
            Pivot.Services.ImageEngine.IThumbnailService? thumbnailService = null)
		{
			_logger = logger;
			_serviceProvider = serviceProvider;
			_repository = repository;
			_messenger = messenger;
			_thumbnailService = thumbnailService;


			// Start periodic flush loop for asset changes (using PeriodicTimer to avoid deadlock)
			_flushLoopCts = new CancellationTokenSource();
			_flushLoopTask = RunFlushLoopAsync(_flushLoopCts.Token);

			// Subscribe to directory changes for real-time sync
			_messenger.Register<DirectoryChangedMessage>(this, async (recipient, message) =>
			{
				try
				{
					await HandleDirectoryChangedAsync(message.Value);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "HandleDirectoryChangedAsync failed");
				}
			});
		}

		/// <summary>
		/// Reconcile database with current settings - delete orphaned assets.
		/// Should be called on startup before scanning.
		/// </summary>
		public async Task ReconcileAsync(IEnumerable<string> validRootPaths, CancellationToken ct = default)
		{
			if (validRootPaths == null) return;

			var validPaths = validRootPaths
				.Where(p => !string.IsNullOrWhiteSpace(p))
				.Select(p => p.Replace('/', '\\').TrimEnd('\\'))
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			_logger.LogInformation("ReconcileAsync: Valid paths = {Paths}", string.Join(", ", validPaths));

			if (validPaths.Count == 0)
			{
				// No valid paths - delete ALL assets
				_logger.LogWarning("ReconcileAsync: No valid directories, clearing all assets from database");
				using var scope = _serviceProvider.CreateScope();
				var context = scope.ServiceProvider.GetRequiredService<Pivot.Data.PivotDbContext>();
				
				var count = await context.Assets.CountAsync(ct);
				if (count > 0)
				{
					context.Assets.RemoveRange(context.Assets);
					await context.SaveChangesAsync(ct);
					_logger.LogInformation("ReconcileAsync: Deleted {Count} orphaned assets", count);
				}
				return;
			}

			// Get all directories from database
			using var repoScope = _serviceProvider.CreateScope();
			var repository = repoScope.ServiceProvider.GetRequiredService<Pivot.Repositories.IAssetRepository>();
			var dbDirectories = await repository.GetAllDirectoriesAsync(ct: ct);

			// Find orphaned directories (not under any valid root)
			var orphanedDirs = dbDirectories
				.Where(dbDir => !validPaths.Any(vp => 
					dbDir.Equals(vp, StringComparison.OrdinalIgnoreCase) ||
					dbDir.StartsWith(vp + "\\", StringComparison.OrdinalIgnoreCase)))
				.ToList();

			_logger.LogInformation("ReconcileAsync: Found {Count} orphaned directories", orphanedDirs.Count);

			// Delete orphaned assets
			int totalDeleted = 0;
			foreach (var orphanDir in orphanedDirs)
			{
				var deleted = await repository.DeleteByDirectoryAsync(orphanDir, ct);
				totalDeleted += deleted;
			}

			if (totalDeleted > 0)
			{
				_logger.LogInformation("ReconcileAsync: Deleted {Count} orphaned assets", totalDeleted);
			}
		}

		public async Task ScanAsync(IEnumerable<string> rootPaths, IProgress<int>? progress = null, CancellationToken cancellationToken = default, HashSet<AssetKind>? allowedKinds = null)
		{

			// 既存のWatcherを完全停止（複数対応）
			StopWatchers();

			foreach (var rootPath in rootPaths)
			{
				if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
				{
					_logger.LogWarning("Root path invalid: {rootPath}", rootPath);
					continue;
				}
				_currentRootPath = rootPath; // 現在のスキャン対象パスを保持

				// Configure concurrency (Simplified for speed)
				int scanDop = Math.Max(2, Environment.ProcessorCount - 1); // Use multiple cores for enumeration/processing
				int hashDop = Math.Max(1, Environment.ProcessorCount / 2); // Limit concurrent hashing
				_fileOpenSemaphore = new SemaphoreSlim(DefaultMaxOpenFiles);
				_hashSemaphore = new SemaphoreSlim(hashDop);

				var allFiles = new List<string>();
				try
				{
					allFiles = Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories).ToList();
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Failed to enumerate files in ScanAsync for root: {rootPath}", rootPath);
				}
				
				int total = allFiles.Count;

				// ===== BATCH PRELOAD: Load existing assets once instead of N queries =====
				Dictionary<string, AssetEntity> existingAssetsCache;
				using (var scope = _serviceProvider.CreateScope())
				{
					var repository = scope.ServiceProvider.GetRequiredService<Pivot.Repositories.IAssetRepository>();
					existingAssetsCache = await repository.GetExistingAssetsInDirectoryAsync(rootPath, cancellationToken);
					_logger.LogInformation("Preloaded {Count} existing assets for batch lookup", existingAssetsCache.Count);
				}

				int processed = 0;
				// Use Parallel.ForEachAsync to control scan parallelism
				await Parallel.ForEachAsync(allFiles, new ParallelOptions { MaxDegreeOfParallelism = scanDop, CancellationToken = cancellationToken }, async (path, ct) =>
				{
					try
					{
						// Use cached lookup instead of DB query per file
						await ProcessFileWithCacheAsync(path, existingAssetsCache, ct, allowedKinds);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Failed to process file during initial scan: {path}", path);
					}
					finally
					{
						Interlocked.Increment(ref processed);
						if (total > 0)
						{
							int percent = (int)Math.Clamp(processed * 100.0 / total, 0, 100);
							progress?.Report(percent);
						}
					}
				});

				progress?.Report(100);
				_logger.LogInformation("Scan completed for {rootPath}", rootPath);
			}
			// 全ルートのスキャンが終わったら複数ウォッチャーを起動
			try { EnsureWatchers(rootPaths); } catch (Exception ex) { _logger.LogError(ex, "EnsureWatchers failed"); }
		}



		// Phase 4: 複数ルート対応（新規）
		private void StopWatchers()
		{
			foreach (var kv in _watchers)
			{
				try
				{
					kv.Value.Created -= OnCreated;
					kv.Value.Changed -= OnChanged;
					kv.Value.Deleted -= OnDeleted;
					kv.Value.Renamed -= OnRenamed;
					kv.Value.Dispose();
				}
				catch { }
			}
			_watchers.Clear();
			_roots = Array.Empty<string>();

			// バックグラウンドの監視処理ループを確実に停止
			try
			{
				_watcherCts?.Cancel();
				_watcherProcessingTask = null;
				_watcherCts?.Dispose();
				_watcherCts = null;
				_pendingEvents.Clear();
			}
			catch { }
		}

        public void EnsureWatchers(IEnumerable<string> rootPaths)
		{
			var desired = new HashSet<string>(rootPaths.Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p)), StringComparer.OrdinalIgnoreCase);

			// remove obsolete
			foreach (var kv in _watchers.Keys)
			{
				if (!desired.Contains(kv))
				{
					if (_watchers.TryRemove(kv, out var w))
					{
						try
						{
							w.Created -= OnCreated;
							w.Changed -= OnChanged;
							w.Deleted -= OnDeleted;
							w.Renamed -= OnRenamed;
							w.Dispose();
						}
						catch { }
					}
				}
			}

			// add new
			foreach (var root in desired)
			{
				if (_watchers.ContainsKey(root)) continue;
				try
				{
					var w = new FileSystemWatcher(root)
					{
						NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
						IncludeSubdirectories = true,
						EnableRaisingEvents = true
					};
					w.Created += OnCreated;
					w.Changed += OnChanged;
					w.Deleted += OnDeleted;
					w.Renamed += OnRenamed;
					_watchers[root] = w;
					_logger.LogInformation("Watcher started for root: {Root}", root);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to start watcher for root: {Root}", root);
				}
			}

			_roots = _watchers.Keys.ToArray();
		}

		private void OnCreated(object sender, FileSystemEventArgs e) => EnqueueWatcherEvent(e.FullPath, e.ChangeType, null);
		private void OnChanged(object sender, FileSystemEventArgs e) => EnqueueWatcherEvent(e.FullPath, e.ChangeType, null);
		private void OnDeleted(object sender, FileSystemEventArgs e) => EnqueueWatcherEvent(e.FullPath, e.ChangeType, null);
		private void OnRenamed(object sender, RenamedEventArgs e) => EnqueueWatcherEvent(e.OldFullPath, e.ChangeType, e.FullPath);

		private async Task DelayAndProcessFile(string path, WatcherChangeTypes changeType, string? newPath = null)
		{
			if (_delayTokens.TryGetValue(path, out var existingCts))
			{
				existingCts.Cancel(); // 既存のディレイをキャンセル
			}

			var newCts = new CancellationTokenSource();
			_delayTokens[path] = newCts;

			try
			{
				await Task.Delay(EventDelayMs, newCts.Token);
				_delayTokens.TryRemove(path, out _);
				await ProcessWatcherEventAsync(path, changeType, newPath, newCts.Token);
			}
			catch (OperationCanceledException) { /* イベント統合によりキャンセルされた */ }
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error delaying file processing: {path}", path);
			}
		}

		private void EnqueueWatcherEvent(string path, WatcherChangeTypes changeType, string? newPath)
		{
			try
			{
				var entry = (changeType, newPath, DateTime.UtcNow);
				_pendingEvents.AddOrUpdate(path, entry, (k, v) => entry);

				// Ensure background processor is running
				if (_watcherProcessingTask == null || _watcherProcessingTask.IsCompleted)
				{
					_watcherCts = new CancellationTokenSource();
					_watcherProcessingTask = Task.Run(() => ProcessPendingWatcherEventsAsync(_watcherCts.Token));
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to enqueue watcher event for {Path}", path);
			}
		}

		private async Task ProcessPendingWatcherEventsAsync(CancellationToken ct)
		{
			try
			{
				while (!ct.IsCancellationRequested)
				{
					var now = DateTime.UtcNow;
					var toProcess = new List<(string Path, WatcherChangeTypes ChangeType, string? NewPath)>();

					foreach (var kv in _pendingEvents)
					{
						if ((now - kv.Value.EnqueuedAt).TotalMilliseconds >= EventDelayMs)
						{
							if (_pendingEvents.TryRemove(kv.Key, out var v))
							{
								toProcess.Add((kv.Key, v.ChangeType, v.NewPath));
							}
						}
					}

					if (toProcess.Count > 0)
					{
						// Process in parallel but bounded
						var tasks = toProcess.Select(item => Task.Run(async () =>
						{
							try
							{
								await ProcessWatcherEventAsync(item.Path, item.ChangeType, item.NewPath, ct);
							}
							catch (Exception ex)
							{
								_logger.LogError(ex, "Error processing watcher batch item: {Path}", item.Path);
							}
						}, ct));

						await Task.WhenAll(tasks);
					}

					await Task.Delay(Math.Max(50, EventDelayMs / 2), ct);
				}
			}
			catch (OperationCanceledException) { }
			catch (Exception ex)
			{
				_logger.LogError(ex, "Watcher processing loop failed");
			}
		}

		
	// ===== NEW SIMPLIFIED FILE PROCESSING =====
	
	/// <summary>
	/// Simplified file processing for initial scan (no watcher events)
	/// </summary>
	private async Task ProcessFileChangeAsync(string path, CancellationToken ct)
	{
		try
		{
            // Use scoped repository for thread safety
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<Pivot.Repositories.IAssetRepository>();

			if (!File.Exists(path))
			{
				_logger.LogWarning("File not found: {Path}", path);
				return;
			}

			var fileInfo = new FileInfo(path);
			string ext = fileInfo.Extension ?? string.Empty;

			// Determine asset kind
			var kind = FileScannerExtensions.DetermineAssetKind(ext);
			if (kind == AssetKind.General)
				return; // Skip non-asset files

			// Quick check: skip if unchanged
			var existing = await repository.GetByPathAsync(path, ct);
			if (existing != null && 
			    existing.FileSize == fileInfo.Length && 
			    existing.LastModifiedUtc == fileInfo.LastWriteTimeUtc)
			{
				return; // No change
			}

			// Hash calculation
			string? hash = existing?.Hash;
			if (existing == null || existing.FileSize != fileInfo.Length)
			{
				_hashSemaphore ??= new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount - 1));
				await _hashSemaphore.WaitAsync(ct);
				try
				{
					// Use SHA256 for fast hashing
					using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096 * 4);
					using var sha = SHA256.Create();
					var hashBytes = await sha.ComputeHashAsync(stream, ct);
					hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
				}
				finally
				{
					_hashSemaphore.Release();
				}
			}

			// Extract metadata for images
			int? width = null;
			int? height = null;
			double? aspectRatio = null;
			string? thumbnailPath = null;
			
			// Code specific metadata
			string? language = null;
			string? tool = null;
			string? contentIndex = null;

			if (kind == AssetKind.Image)
			{
				try
				{
					var imageInfo = await SixLabors.ImageSharp.Image.IdentifyAsync(path);
					if (imageInfo != null)
					{
						width = imageInfo.Width;
						height = imageInfo.Height;
						aspectRatio = height > 0 ? (double)width / height : null;
					}
				}
				catch (Exception dimEx)
				{
					_logger.LogDebug(dimEx, "Failed to read image dimensions: {Path}", path);
				}

				// Generate thumbnail
				if (_thumbnailService != null)
				{
					try
					{
						thumbnailPath = await _thumbnailService.GenerateThumbnailAsync(path, hash ?? "", 256);
					}
					catch (Exception thumbEx)
					{
						_logger.LogDebug(thumbEx, "Failed to generate thumbnail: {Path}", path);
					}
				}
			}
			else if (kind == AssetKind.Script || kind == AssetKind.Code)
			{
				try
				{
					// Index content for Full Text Search (limit to 1MB to prevent OOM)
					if (fileInfo.Length < 1024 * 1024) 
					{
						contentIndex = await File.ReadAllTextAsync(path, ct);
					}
					language = ext.TrimStart('.').ToLowerInvariant();
					// Tool logic can be refined later
				}
				catch (Exception codeEx)
				{
					_logger.LogDebug(codeEx, "Failed to read code content: {Path}", path);
				}
			}

			// Create or update AssetEntity
			var asset = new AssetEntity
			{
				FilePath = path,
				FileName = Path.GetFileName(path),
				Directory = Path.GetDirectoryName(path) ?? string.Empty,
				Extension = ext,
				FileSize = fileInfo.Length,
				LastModifiedUtc = fileInfo.LastWriteTimeUtc,
				Hash = hash,
				Kind = kind,
				Width = width,
				Height = height,
				AspectRatio = aspectRatio,
				ThumbnailPath = thumbnailPath,
				ThumbnailGeneratedAt = thumbnailPath != null ? DateTime.UtcNow : null,
				Language = language,
				Tool = tool,
				ContentIndex = contentIndex
			};

			await repository.UpsertAsync(asset, ct);

			// Notify UI
			var changeType = existing == null 
				? ItemChangeData<AssetEntity>.ChangeType.Added 
				: ItemChangeData<AssetEntity>.ChangeType.Updated;
			_assetChangeBuffer.Enqueue(new ItemChangeData<AssetEntity>(changeType, asset));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing file: {Path}", path);
		}
	}

	/// <summary>
	/// Optimized file processing for initial scan using pre-loaded cache (eliminates N+1 queries).
	/// </summary>
	private async Task ProcessFileWithCacheAsync(string path, Dictionary<string, AssetEntity> existingAssetsCache, CancellationToken ct, HashSet<AssetKind>? allowedKinds = null)
	{
		try
		{
			// Use scoped repository for DB writes only
			using var scope = _serviceProvider.CreateScope();
			var repository = scope.ServiceProvider.GetRequiredService<Pivot.Repositories.IAssetRepository>();

			if (!File.Exists(path))
			{
				return; // Silently skip missing files during initial scan
			}

			var fileInfo = new FileInfo(path);
			string ext = fileInfo.Extension ?? string.Empty;

			// Determine asset kind
			var kind = FileScannerExtensions.DetermineAssetKind(ext);
			if (kind == AssetKind.General)
				return; // Skip non-asset files
			
			// Filter by allowed kinds if specified
			if (allowedKinds != null && !allowedKinds.Contains(kind))
			{
				System.Diagnostics.Debug.WriteLine($"[Scan] SKIPPED {path} - Kind {kind} not allowed");
				return; // Skip files not in allowed kinds
			}

			// Quick check: O(1) lookup from pre-loaded cache instead of DB query
			existingAssetsCache.TryGetValue(path, out var existing);
			if (existing != null && 
			    existing.FileSize == fileInfo.Length && 
			    existing.LastModifiedUtc == fileInfo.LastWriteTimeUtc)
			{
				return; // No change
			}

			// Hash calculation
			string? hash = existing?.Hash;
			if (existing == null || existing.FileSize != fileInfo.Length)
			{
				_hashSemaphore ??= new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount - 1));
				await _hashSemaphore.WaitAsync(ct);
				try
				{
					using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096 * 4);
					using var sha = SHA256.Create();
					var hashBytes = await sha.ComputeHashAsync(stream, ct);
					hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
				}
				finally
				{
					_hashSemaphore.Release();
				}
			}

			// Extract metadata for images
			int? width = null;
			int? height = null;
			double? aspectRatio = null;
			string? thumbnailPath = null;
			
			// Code specific metadata
			string? language = null;
			string? tool = null;
			string? contentIndex = null;

			if (kind == AssetKind.Image)
			{
				try
				{
					var imageInfo = await SixLabors.ImageSharp.Image.IdentifyAsync(path);
					if (imageInfo != null)
					{
						width = imageInfo.Width;
						height = imageInfo.Height;
						aspectRatio = height > 0 ? (double)width / height : null;
					}
				}
				catch (Exception dimEx)
				{
					_logger.LogDebug(dimEx, "Failed to read image dimensions: {Path}", path);
				}

				// Generate thumbnail
				if (_thumbnailService != null)
				{
					try
					{
						thumbnailPath = await _thumbnailService.GenerateThumbnailAsync(path, hash ?? "", 256);
					}
					catch (Exception thumbEx)
					{
						_logger.LogDebug(thumbEx, "Failed to generate thumbnail: {Path}", path);
					}
				}
			}
			else if (kind == AssetKind.Script || kind == AssetKind.Code)
			{
				try
				{
					if (fileInfo.Length < 1024 * 1024) 
					{
						contentIndex = await File.ReadAllTextAsync(path, ct);
					}
					language = ext.TrimStart('.').ToLowerInvariant();
				}
				catch (Exception codeEx)
				{
					_logger.LogDebug(codeEx, "Failed to read code content: {Path}", path);
				}
			}

			// Create or update AssetEntity
			var asset = new AssetEntity
			{
				FilePath = path,
				FileName = Path.GetFileName(path),
				Directory = Path.GetDirectoryName(path) ?? string.Empty,
				Extension = ext,
				FileSize = fileInfo.Length,
				LastModifiedUtc = fileInfo.LastWriteTimeUtc,
				Hash = hash,
				Kind = kind,
				Width = width,
				Height = height,
				AspectRatio = aspectRatio,
				ThumbnailPath = thumbnailPath,
				ThumbnailGeneratedAt = thumbnailPath != null ? DateTime.UtcNow : null,
				Language = language,
				Tool = tool,
				ContentIndex = contentIndex
			};

			await repository.UpsertAsync(asset, ct);

			// Notify UI
			var changeType = existing == null 
				? ItemChangeData<AssetEntity>.ChangeType.Added 
				: ItemChangeData<AssetEntity>.ChangeType.Updated;
			_assetChangeBuffer.Enqueue(new ItemChangeData<AssetEntity>(changeType, asset));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing file with cache: {Path}", path);
		}
	}

	/// <summary>
	/// Process watcher events (Created, Changed, Deleted, Renamed) safely.
	/// </summary>
	private async Task ProcessWatcherEventAsync(string path, WatcherChangeTypes changeType, string? newPath, CancellationToken ct)
	{
		// Handle deletion
		if (changeType == WatcherChangeTypes.Deleted)
		{
			await _repository.MarkDeletedAsync(path, ct);
			_logger.LogInformation("Marked as deleted: {Path}", path);
			
			var deletedEntity = new AssetEntity { FilePath = path };
			_assetChangeBuffer.Enqueue(new ItemChangeData<AssetEntity>(ItemChangeData<AssetEntity>.ChangeType.Deleted, deletedEntity));
			return;
		}

		// Handle rename
		if (changeType == WatcherChangeTypes.Renamed && newPath != null)
		{
			await _repository.MarkDeletedAsync(path, ct);
			path = newPath;
		}

		// Process file (reuse scan logic)
		await ProcessFileChangeAsync(path, ct);
	}

	/// <summary>
	/// Periodic flush loop using PeriodicTimer (C# 10+) to avoid deadlock issues with System.Timers.Timer.
	/// </summary>
	private async Task RunFlushLoopAsync(CancellationToken ct)
	{
		try
		{
			using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(AssetFlushIntervalMs));
			while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
			{
				try
				{
					FlushAssetBuffer();
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "FlushAssetBuffer failed");
				}
			}
		}
		catch (OperationCanceledException)
		{
			// Expected when disposed
		}
	}

	private void FlushAssetBuffer()
	{
		var batch = new List<ItemChangeData<AssetEntity>>();
		while (batch.Count < AssetFlushBatchMax && _assetChangeBuffer.TryDequeue(out var item))
		{
			batch.Add(item);
		}
		if (batch.Count == 0) return;
		_messenger.Send(new BulkItemsChangedMessage<AssetEntity>(batch));
	}



		public void Dispose()
		{
			StopWatchers();
			foreach (var cts in _delayTokens.Values) 
			{
				try { cts.Dispose(); } catch { }
			}
			_delayTokens.Clear();
			try { _fileOpenSemaphore?.Dispose(); } catch { }
			try { _hashSemaphore?.Dispose(); } catch { }
			// Stop flush loop gracefully
			try
			{
				_flushLoopCts?.Cancel();
				_flushLoopTask?.Wait(TimeSpan.FromSeconds(1));
				_flushLoopCts?.Dispose();
			}
			catch { }
			GC.SuppressFinalize(this);
		}


		/// <summary>
		/// Handle directory settings changes (add/remove directories).
		/// </summary>
		private async Task HandleDirectoryChangedAsync(DirectoryChangedMessageData change)
		{
			_logger.LogInformation("Directory changed: {Type} - {Path}", change.Type, change.Path);

			if (change.Type == DirectoryChangedMessageData.ChangeType.Added)
			{
				// Start scanning the new directory
				if (Directory.Exists(change.Path))
				{
					await ScanAsync(new[] { change.Path });
				}
			}
			else if (change.Type == DirectoryChangedMessageData.ChangeType.Removed)
			{
				// Stop watching and delete assets from database
				StopWatcherForPath(change.Path);

				using var scope = _serviceProvider.CreateScope();
				var repository = scope.ServiceProvider.GetRequiredService<Pivot.Repositories.IAssetRepository>();
				var deletedCount = await repository.DeleteByDirectoryAsync(change.Path);
				_logger.LogInformation("Deleted {Count} assets from removed directory: {Path}", deletedCount, change.Path);

				// Notify UI about bulk removal
				_messenger.Send(new DirectoryRemovedMessage(change.Path));
			}
		}

		/// <summary>
		/// Stop watcher for a specific path.
		/// </summary>
		private void StopWatcherForPath(string path)
		{
			var normalizedPath = path.Replace('/', '\\').TrimEnd('\\');
			if (_watchers.TryRemove(normalizedPath, out var watcher))
			{
				try
				{
					watcher.Created -= OnCreated;
					watcher.Changed -= OnChanged;
					watcher.Deleted -= OnDeleted;
					watcher.Renamed -= OnRenamed;
					watcher.Dispose();
					_logger.LogInformation("Stopped watcher for removed directory: {Path}", path);
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Error stopping watcher for {Path}", path);
				}
			}
		}


	}
}
