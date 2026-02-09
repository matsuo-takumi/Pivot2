using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pivot.Engine.Data;
using Pivot.Engine.Messages;
using Pivot.Engine.Models;
using Pivot.Engine.Repositories;
using Pivot.Engine.Utilities;

namespace Pivot.Engine.Services
{
	public class FileScannerService : IDisposable
	{
		private readonly ILogger _logger;
		private readonly IMessenger _messenger;
		private readonly IDbContextFactory<PivotDbContext> _dbFactory;

		
		private string? _currentRootPath;
		private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
		private IReadOnlyCollection<string> _roots = Array.Empty<string>();
		private CancellationTokenSource? _watcherCts;
		private Task? _watcherProcessingTask;
		private readonly ConcurrentDictionary<string, (WatcherChangeTypes ChangeType, string? NewPath, DateTime EnqueuedAt)> _pendingEvents = new();

		private readonly ConcurrentDictionary<string, CancellationTokenSource> _delayTokens = new();
		private const int EventDelayMs = 500;

		private static readonly HashSet<string> AssetExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga", ".tif", ".tiff", ".webp",
			".fbx", ".obj", ".usd", ".usdz", ".gltf", ".glb", ".hdr", ".exr"
		};

		private const int DefaultMaxOpenFiles = 64;
		private SemaphoreSlim? _fileOpenSemaphore;
		private SemaphoreSlim? _hashSemaphore;

		private readonly ConcurrentQueue<ItemChangeData<AssetEntity>> _assetChangeBuffer = new();
		private CancellationTokenSource? _flushLoopCts;
		private Task? _flushLoopTask;
		private const int AssetFlushIntervalMs = 300;
		private const int AssetFlushBatchMax = 100;

		public Func<string, int, int, CancellationToken, Task<string>>? ThumbnailGenerator { get; set; }

		public FileScannerService(
			ILogger<FileScannerService> logger, 
			IMessenger messenger, 
			IDbContextFactory<PivotDbContext> dbFactory)
		{
			_logger = logger;
			_messenger = messenger;
			_dbFactory = dbFactory;

			_flushLoopCts = new CancellationTokenSource();
			_flushLoopTask = RunFlushLoopAsync(_flushLoopCts.Token);

            // Register for DirectoryChangedMessage provided by Engine/UI
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
				_logger.LogWarning("ReconcileAsync: No valid directories, clearing all assets from database");
				using var context = await _dbFactory.CreateDbContextAsync(ct);
				
				var count = await context.Assets.CountAsync(ct);
				if (count > 0)
				{
					context.Assets.RemoveRange(context.Assets);
					await context.SaveChangesAsync(ct);
					_logger.LogInformation("ReconcileAsync: Deleted {Count} orphaned assets", count);
				}
				return;
			}

			// orphaned check
            using (var context = await _dbFactory.CreateDbContextAsync(ct))
            {
                var repo = new AssetRepository(context);
			    var dbDirectories = await repo.GetAllDirectoriesAsync(ct: ct);
			    
			    var orphanedDirs = dbDirectories
				    .Where(dbDir => !validPaths.Any(vp => 
					    dbDir.Equals(vp, StringComparison.OrdinalIgnoreCase) ||
					    dbDir.StartsWith(vp + "\\", StringComparison.OrdinalIgnoreCase)))
				    .ToList();

			    _logger.LogInformation("ReconcileAsync: Found {Count} orphaned directories", orphanedDirs.Count);

			    int totalDeleted = 0;
			    foreach (var orphanDir in orphanedDirs)
			    {
				    var deleted = await repo.DeleteByDirectoryAsync(orphanDir, ct);
				    totalDeleted += deleted;
			    }

			    if (totalDeleted > 0)
			    {
				    _logger.LogInformation("ReconcileAsync: Deleted {Count} orphaned assets", totalDeleted);
			    }
            }
		}

		public async Task ScanAsync(IEnumerable<string> rootPaths, IProgress<int>? progress = null, CancellationToken cancellationToken = default, HashSet<AssetKind>? allowedKinds = null)
		{
			StopWatchers();

			foreach (var rootPath in rootPaths)
			{
				if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
				{
					_logger.LogWarning("Root path invalid: {rootPath}", rootPath);
					continue;
				}
				_currentRootPath = rootPath;

				int scanDop = Math.Max(2, Environment.ProcessorCount - 1);
				int hashDop = Math.Max(1, Environment.ProcessorCount / 2);
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

				Dictionary<string, AssetEntity> existingAssetsCache;
                // Preload cache
                using (var context = await _dbFactory.CreateDbContextAsync(cancellationToken))
                {
                    var repo = new AssetRepository(context);
					existingAssetsCache = await repo.GetExistingAssetsInDirectoryAsync(rootPath, cancellationToken);
					_logger.LogInformation("Preloaded {Count} existing assets for batch lookup", existingAssetsCache.Count);
				}

				int processed = 0;
				await Parallel.ForEachAsync(allFiles, new ParallelOptions { MaxDegreeOfParallelism = scanDop, CancellationToken = cancellationToken }, async (path, ct) =>
				{
					try
					{
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
			try { EnsureWatchers(rootPaths); } catch (Exception ex) { _logger.LogError(ex, "EnsureWatchers failed"); }
		}

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

		private void EnqueueWatcherEvent(string path, WatcherChangeTypes changeType, string? newPath)
		{
			try
			{
				var entry = (changeType, newPath, DateTime.UtcNow);
				_pendingEvents.AddOrUpdate(path, entry, (k, v) => entry);

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

		private async Task ProcessFileChangeAsync(string path, CancellationToken ct)
		{
			try
			{
                using var context = await _dbFactory.CreateDbContextAsync(ct);
                var repository = new AssetRepository(context);

				if (!File.Exists(path)) return;

				var fileInfo = new FileInfo(path);
				string ext = fileInfo.Extension ?? string.Empty;

				var kind = FileScannerExtensions.DetermineAssetKind(ext);
				if (kind == AssetKind.General) return;

				var existing = await repository.GetByPathAsync(path, ct);
				if (existing != null && 
				    existing.FileSize == fileInfo.Length && 
				    existing.LastModifiedUtc == fileInfo.LastWriteTimeUtc)
				{
					return;
				}

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

				int? width = null;
				int? height = null;
				double? aspectRatio = null;
				string? thumbnailPath = null;
				string? language = null;
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
					catch { }

					try
					{
                        // Generate thumbnail via Engine
						if (ThumbnailGenerator != null)
                        {
                            thumbnailPath = await ThumbnailGenerator(path, 300, 200, ct);
                        }
					}
					catch { }
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
					catch { }
				}

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
					ContentIndex = contentIndex
				};

				await repository.UpsertAsync(asset, ct);

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

		private async Task ProcessFileWithCacheAsync(string path, Dictionary<string, AssetEntity> existingAssetsCache, CancellationToken ct, HashSet<AssetKind>? allowedKinds = null)
		{
			try
			{
                using var context = await _dbFactory.CreateDbContextAsync(ct);
                var repository = new AssetRepository(context);

				if (!File.Exists(path)) return;

				var fileInfo = new FileInfo(path);
				string ext = fileInfo.Extension ?? string.Empty;

				var kind = FileScannerExtensions.DetermineAssetKind(ext);
				if (kind == AssetKind.General) return;
				
				if (allowedKinds != null && !allowedKinds.Contains(kind)) return;

				existingAssetsCache.TryGetValue(path, out var existing);
				if (existing != null && 
				    existing.FileSize == fileInfo.Length && 
				    existing.LastModifiedUtc == fileInfo.LastWriteTimeUtc)
				{
					return;
				}

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

				int? width = null;
				int? height = null;
				double? aspectRatio = null;
				string? thumbnailPath = null;
				string? language = null;
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
					catch { }

					try
					{
                        // Generate thumbnail via Engine (which will queue if needed and return empty string)
						if (ThumbnailGenerator != null)
                        {
                            thumbnailPath = await ThumbnailGenerator(path, 300, 200, ct);
                        }
					}
					catch { }
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
					catch { }
				}

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
					ContentIndex = contentIndex
				};

				await repository.UpsertAsync(asset, ct);

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

		private async Task ProcessWatcherEventAsync(string path, WatcherChangeTypes changeType, string? newPath, CancellationToken ct)
		{
            using var context = await _dbFactory.CreateDbContextAsync(ct);
            var repository = new AssetRepository(context);

			if (changeType == WatcherChangeTypes.Deleted)
			{
				await repository.MarkDeletedAsync(path, ct);
				_logger.LogInformation("Marked as deleted: {Path}", path);
				
				var deletedEntity = new AssetEntity { FilePath = path };
				_assetChangeBuffer.Enqueue(new ItemChangeData<AssetEntity>(ItemChangeData<AssetEntity>.ChangeType.Deleted, deletedEntity));
				return;
			}

			if (changeType == WatcherChangeTypes.Renamed && newPath != null)
			{
				await repository.MarkDeletedAsync(path, ct);
				path = newPath;
			}

			await ProcessFileChangeAsync(path, ct);
		}

		private async Task HandleDirectoryChangedAsync(DirectoryChangedMessageData data)
        {
            // If needed, handle logic when directory is added/removed from settings
            if (data.Type == DirectoryChangedMessageData.ChangeType.Removed)
            {
                // Optionally stop watcher for specific path if not handled by ScanAsync logic
                // But ScanAsync(allPaths) usually re-syncs everything.
            }
        }

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
			
            _flushLoopCts?.Cancel();
            _flushLoopCts?.Dispose();
            _messenger.UnregisterAll(this);
		}
	}
}
