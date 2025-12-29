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
using SixLabors.ImageSharp;

namespace Pivot.Services
{
	public class FileScannerService : IDisposable
	{
		private readonly ILogger<FileScannerService> _logger;
		private readonly MetadataService _metadataService;
		private readonly ICatalogService? _catalogService; // scanモード用
		private readonly IThumbnailService? _thumbnailService;
		private readonly bool _useScanMode;
		private readonly IMessenger _messenger;
		private FileSystemWatcher? _watcher;
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
		private readonly ConcurrentQueue<ItemChangeData<AssetEntry>> _assetChangeBuffer = new();
		private System.Timers.Timer? _assetFlushTimer;
		private const int AssetFlushIntervalMs = 300; // Phase 6: move to config
		private const int AssetFlushBatchMax = 100;   // Phase 6: move to config

		public FileScannerService(ILogger<FileScannerService> logger, MetadataService metadataService, IMessenger messenger, IConfiguration? configuration = null, ICatalogService? catalogService = null, IThumbnailService? thumbnailService = null)
		{
			_logger = logger;
			_metadataService = metadataService;
			_messenger = messenger;
			_thumbnailService = thumbnailService;
			_useScanMode = bool.TryParse(configuration?["AppSettings:UseScanMode"], out var flag) && flag;
			_catalogService = catalogService;

			// Start periodic flush timer for asset changes
			_assetFlushTimer = new System.Timers.Timer(AssetFlushIntervalMs)
			{
				AutoReset = true,
				Enabled = true
			};
			_assetFlushTimer.Elapsed += (_, __) =>
			{
				try { FlushAssetBuffer(); }
				catch (Exception ex) { _logger.LogError(ex, "FlushAssetBuffer failed"); }
			};
		}

		public async Task ScanAsync(IEnumerable<string> rootPaths, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
		{
			if (_useScanMode)
			{
				try
				{
					var catalog = _catalogService ?? new JsonCatalogService();
					await catalog.InitializeAsync(rootPaths, cancellationToken);
					progress?.Report(100);
					_logger.LogInformation("ScanAsync: UseScanMode enabled. Delegated to JsonCatalogService.");
					return;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "ScanAsync: JsonCatalogService initialization failed in UseScanMode.");
					// フォールバック: 従来経路
				}
			}

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

				int processed = 0;
				// Use Parallel.ForEachAsync to control scan parallelism
				await Parallel.ForEachAsync(allFiles, new ParallelOptions { MaxDegreeOfParallelism = scanDop, CancellationToken = cancellationToken }, async (path, ct) =>
				{
					try
					{
						await ProcessFileChange(path, WatcherChangeTypes.Created, null, ct);
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

		private void StartWatching(string path)
		{
			try
			{
				_watcher = new FileSystemWatcher(path)
				{
					NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
					IncludeSubdirectories = true,
					EnableRaisingEvents = true
				};

				_watcher.Created += OnCreated;
				_watcher.Changed += OnChanged;
				_watcher.Deleted += OnDeleted;
				_watcher.Renamed += OnRenamed;

				_logger.LogInformation("Started watching directory: {path}", path);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to start FileSystemWatcher for path: {path}", path);
				StopWatching(); // 失敗した場合は停止
			}
		}

		private void StopWatching()
		{
			if (_watcher != null)
			{
				_watcher.Created -= OnCreated;
				_watcher.Changed -= OnChanged;
				_watcher.Deleted -= OnDeleted;
				_watcher.Renamed -= OnRenamed;

				_watcher.Dispose();
				_watcher = null;
				_logger.LogInformation("Stopped watching directory.");
				// Cancel background processor
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
				await ProcessFileChange(path, changeType, newPath, newCts.Token);
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
								await ProcessFileChange(item.Path, item.ChangeType, item.NewPath, ct);
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

		private async Task ProcessFileChange(string path, WatcherChangeTypes changeType, string? newPath = null, CancellationToken cancellationToken = default)
		{
			try
			{
				_logger.LogInformation("File system event: {ChangeType} - {Path}", changeType, path);

				if (changeType == WatcherChangeTypes.Deleted)
				{
					await _metadataService.DeleteFileAsync(path);
					_logger.LogInformation("Deleted metadata for: {Path}", path);
					// enqueue for bulk
					_assetChangeBuffer.Enqueue(new ItemChangeData<AssetEntry>(ItemChangeData<AssetEntry>.ChangeType.Deleted, new AssetEntry { Path = path }));
					// keep legacy single message for backward compatibility (AssetViewModel already supports it)
					_messenger.Send(new AssetChangedMessage(new AssetChangedMessageData(AssetChangedMessageData.ChangeType.Deleted, path)));
				}
				else // Created, Changed, Renamed
				{
					// Renamedの場合、古いパスのメタデータを削除し、新しいパスで作成/更新
					if (changeType == WatcherChangeTypes.Renamed && newPath != null)
					{
						await _metadataService.DeleteFileAsync(path); // 古いパスを削除
						path = newPath; // 新しいパスで処理を続行
					}

					if (!File.Exists(path))
					{
						_logger.LogWarning("File not found after event: {Path}", path);
						return; // ファイルが存在しない場合はスキップ
					}

					var fileInfo = new FileInfo(path);
					string ext = fileInfo.Extension ?? string.Empty;
					string type = GuessMime(ext);

					// Quick check: compare size and mtime to skip unnecessary hashing
					FileEntry? existingFileEntry = null;
					bool needsHash = true;
					// Quick check: compare size and mtime to skip unnecessary hashing
					existingFileEntry = await _metadataService.GetFileEntryByPathAsync(path);
					if (existingFileEntry != null && existingFileEntry.Size == fileInfo.Length && existingFileEntry.UpdatedAt == fileInfo.LastWriteTimeUtc)
					{
						needsHash = false; // no content change
					}

					// Ensure semaphores exist (for watcher-triggered events)
					_fileOpenSemaphore ??= new SemaphoreSlim(DefaultMaxOpenFiles);
					_hashSemaphore ??= new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount - 1));

					string hash = existingFileEntry?.Hash ?? string.Empty;
					if (needsHash)
					{
						// Acquire semaphores to limit open files and concurrent hash CPU usage
						await _fileOpenSemaphore.WaitAsync(cancellationToken);
						try
						{
							await _hashSemaphore.WaitAsync(cancellationToken);
							try
							{
								int tries = 0;
								while (true)
								{
									try
									{
										hash = await ComputeXxHash64Async(path, cancellationToken);
										break;
									}
									catch (IOException) when (++tries <= DefaultRetryCount)
									{
										await Task.Delay(DefaultRetryDelayMs, cancellationToken);
									}
								}
							}
							finally
							{
								_hashSemaphore.Release();
							}
						}
						finally
						{
							_fileOpenSemaphore.Release();
						}
					}

					await _metadataService.UpsertFileAsync(path, type, fileInfo.Length, fileInfo.LastWriteTimeUtc, hash);
					_logger.LogInformation("Upserted metadata for: {Path}", path);

					// Update scan cache
					try
					{
						// 複数ルート対応: このパスに合致するルートを推定
						string rootForPath = _currentRootPath ?? string.Empty;
						if (_roots.Count > 0)
						{
							rootForPath = _roots.FirstOrDefault(r => path.StartsWith(r, StringComparison.OrdinalIgnoreCase)) ?? rootForPath;
						}
						var cacheEntry = new ScanCacheEntry(path, hash, fileInfo.Length, fileInfo.LastWriteTimeUtc, rootForPath);
						await _metadataService.UpsertScanCacheAsync(cacheEntry);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Failed to update scan cache for: {Path}", path);
					}

					// Asset integration: create or update AssetEntry when file looks like an asset
					try
					{
						if (IsAssetExtension(ext))
						{
							var fileEntry = await _metadataService.GetFileEntryByPathAsync(path);
							if (fileEntry != null)
							{
								var existingAsset = await _metadataService.GetAssetEntryByPathAsync(path);
								if (existingAsset != null)
								{
									existingAsset.Name = Path.GetFileName(path);
									existingAsset.Type = type;
									existingAsset.Size = fileInfo.Length;
									existingAsset.Hash = hash;
									existingAsset.File = fileEntry;
									
									// Fix missing dimensions on existing assets
									if (ImageExtensions.Contains(ext) && existingAsset.PixelWidth == 0)
									{
										try
										{
											var imageInfo = await Image.IdentifyAsync(path);
											if (imageInfo != null)
											{
												existingAsset.PixelWidth = imageInfo.Width;
												existingAsset.PixelHeight = imageInfo.Height;
											}
										}
										catch (Exception dimEx)
										{
											_logger.LogDebug(dimEx, "Failed to read image dimensions for existing asset: {Path}", path);
										}
									}
									
									existingAsset.UpdatedAt = DateTime.UtcNow;
									await _metadataService.UpdateAssetEntryAsync(existingAsset);
									// enqueue bulk update and keep legacy single message
									_assetChangeBuffer.Enqueue(new ItemChangeData<AssetEntry>(ItemChangeData<AssetEntry>.ChangeType.Updated, existingAsset));
									_messenger.Send(new AssetChangedMessage(new AssetChangedMessageData(AssetChangedMessageData.ChangeType.Updated, path)));
								}
								else
								{
									// Generate thumbnail for image files
									string? thumbnailCachePath = null;
									int pixelWidth = 0;
									int pixelHeight = 0;
									
									if (ImageExtensions.Contains(ext))
									{
										// Extract image dimensions (fast header-only read)
										try
										{
											var imageInfo = await Image.IdentifyAsync(path);
											if (imageInfo != null)
											{
												pixelWidth = imageInfo.Width;
												pixelHeight = imageInfo.Height;
											}
										}
										catch (Exception dimEx)
										{
											_logger.LogDebug(dimEx, "Failed to read image dimensions for: {Path}", path);
										}
										
										// Generate thumbnail
										if (_thumbnailService != null)
										{
											try
											{
												thumbnailCachePath = await _thumbnailService.GetOrCreateThumbnailAsync(path, 300, 200);
											}
											catch (Exception thumbEx)
											{
												_logger.LogDebug(thumbEx, "Failed to generate thumbnail for: {Path}", path);
											}
										}
									}
									
									var newAsset = new AssetEntry
									{
										Name = Path.GetFileName(path),
										Type = type,
										Path = path,
										Size = fileInfo.Length,
										Hash = hash,
										File = fileEntry,
										ThumbnailCachePath = thumbnailCachePath,
										PixelWidth = pixelWidth,
										PixelHeight = pixelHeight,
										CreatedAt = DateTime.UtcNow,
										UpdatedAt = DateTime.UtcNow
									};
									await _metadataService.AddAssetEntryAsync(newAsset);
									// enqueue bulk add and keep legacy single message
									_assetChangeBuffer.Enqueue(new ItemChangeData<AssetEntry>(ItemChangeData<AssetEntry>.ChangeType.Added, newAsset));
									_messenger.Send(new AssetChangedMessage(new AssetChangedMessageData(AssetChangedMessageData.ChangeType.Added, path)));
								}
							}
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Failed to upsert asset entry for: {Path}", path);
					}
				}
			}
			catch (OperationCanceledException) { /* スキャンキャンセル */ }
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing file system event: {Path}", path);
			}
		}

		// Flush asset buffer to bulk message
		private void FlushAssetBuffer()
		{
			var batch = new List<ItemChangeData<AssetEntry>>();
			while (batch.Count < AssetFlushBatchMax && _assetChangeBuffer.TryDequeue(out var item))
			{
				batch.Add(item);
			}
			if (batch.Count == 0) return;
			_messenger.Send(new BulkItemsChangedMessage<AssetEntry>(batch));
		}

		/// <summary>
		/// ファイルが前回のスキャン結果と比較して変更されているかチェックします。
		/// スキャンキャッシュを利用して、重複したハッシュ計算を避けます。
		/// </summary>
		private async Task<bool> HasFileChangedAsync(string path, CancellationToken cancellationToken)
		{
			try
			{
				var fileInfo = new FileInfo(path);
				var cacheEntry = await _metadataService.GetScanCacheAsync(path);

				if (cacheEntry == null)
				{
					_logger.LogDebug("No cache entry found for: {Path}", path);
					return true; // キャッシュなし = 新規ファイル = 変更あり
				}

				// サイズと最終更新時刻が同じなら、キャッシュされたハッシュを信頼
				if (cacheEntry.Size == fileInfo.Length && cacheEntry.LastModifiedUtc == fileInfo.LastWriteTimeUtc)
				{
					_logger.LogDebug("File unchanged (cache hit): {Path}", path);
					return false; // 変更なし
				}

				_logger.LogDebug("File changed (cache miss): {Path}, Size: {Size} -> {NewSize}, LastModified: {LastMod} -> {NewLastMod}", 
					path, cacheEntry.Size, fileInfo.Length, cacheEntry.LastModifiedUtc, fileInfo.LastWriteTimeUtc);
				return true; // 変更あり
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Error checking file cache status for: {Path}", path);
				return true; // エラー時は安全側（変更ありとして処理）
			}
		}

		public async Task ScanWithCacheAsync(IEnumerable<string> rootPaths, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
		{
			if (_useScanMode)
			{
				try
				{
					var catalog = _catalogService ?? new JsonCatalogService();
					await catalog.InitializeAsync(rootPaths, cancellationToken);
					progress?.Report(100);
					_logger.LogInformation("ScanWithCacheAsync: UseScanMode enabled. Delegated to JsonCatalogService.");
					return;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "ScanWithCacheAsync: JsonCatalogService initialization failed in UseScanMode.");
					// フォールバック: 従来経路
				}
			}

			// 既存のWatcherを完全停止（複数対応）
			StopWatchers();

			foreach (var rootPath in rootPaths)
			{
				if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
				{
					_logger.LogWarning("Root path invalid: {rootPath}", rootPath);
					continue;
				}

				_currentRootPath = rootPath;
				_logger.LogInformation("Starting scan with cache optimization for: {RootPath}", rootPath);

				// Configure concurrency (Simplified for speed)
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
				    _logger.LogWarning(ex, "Failed to enumerate files for root: {rootPath}", rootPath);
				}
				
				int total = allFiles.Count;
				int processed = 0;
				int changedCount = 0;

				// First pass: identify changed files using cache
				var changedFiles = new System.Collections.Concurrent.ConcurrentBag<string>();
				await Parallel.ForEachAsync(allFiles, new ParallelOptions { MaxDegreeOfParallelism = scanDop, CancellationToken = cancellationToken }, async (path, ct) =>
				{
					try
					{
						if (await HasFileChangedAsync(path, ct))
						{
							changedFiles.Add(path);
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error checking cache status for file: {path}", path);
					}
					finally
					{
						Interlocked.Increment(ref processed);
						if (total > 0)
						{
							int percent = (int)Math.Clamp(processed * 50.0 / total, 0, 50); // First 50% for cache check
							progress?.Report(percent);
						}
					}
				});

				// Second pass: process changed files
				changedCount = changedFiles.Count;
				_logger.LogInformation("Found {ChangedCount} changed files out of {TotalCount}", changedCount, total);
				processed = 0;

				await Parallel.ForEachAsync(changedFiles, new ParallelOptions { MaxDegreeOfParallelism = scanDop, CancellationToken = cancellationToken }, async (path, ct) =>
				{
					try
					{
						await ProcessFileChange(path, WatcherChangeTypes.Created, null, ct);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Failed to process changed file: {path}", path);
					}
					finally
					{
						Interlocked.Increment(ref processed);
						if (changedCount > 0)
						{
							int percent = (int)Math.Clamp(50 + processed * 50.0 / changedCount, 50, 100); // Second 50% for processing
							progress?.Report(percent);
						}
					}
				});

				progress?.Report(100);
				_logger.LogInformation("Scan with cache completed for {rootPath}. Processed {ChangedCount} changed files.", rootPath, changedCount);
			}
			// 全ルートのスキャンが終わったら複数ウォッチャーを起動
			try { EnsureWatchers(rootPaths); } catch (Exception ex) { _logger.LogError(ex, "EnsureWatchers failed"); }
		}

		public void Dispose()
		{
			// 監視を完全停止（複数対応）
			StopWatchers();
			foreach (var cts in _delayTokens.Values) cts.Dispose();
			_delayTokens.Clear();
			try { _fileOpenSemaphore?.Dispose(); } catch { }
			try { _hashSemaphore?.Dispose(); } catch { }
			try { if (_assetFlushTimer != null) { _assetFlushTimer.Dispose(); _assetFlushTimer = null; } } catch { }
			GC.SuppressFinalize(this);
		}

		private static string GuessMime(string ext)
		{
			if (ImageExtensions.Contains(ext)) return "image/*";
			switch (ext.ToLowerInvariant())
			{
				case ".mp4":
				case ".mov":
				case ".avi": return "video/*";
				case ".pdf": return "application/pdf";
				case ".fbx":
				case ".obj":
				case ".usd": return "model/3d";
				default: return "application/octet-stream";
			}
		}

		private static async Task<string> ComputeMD5Async(string path, CancellationToken ct)
		{
			using var md5 = MD5.Create();
			await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
			var hash = await md5.ComputeHashAsync(stream, ct);
			return Convert.ToHexString(hash);
		}

		private static async Task<string> ComputeXxHash64Async(string path, CancellationToken ct)
		{
			await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
			var hasher = new XxHash64();
			var buffer = new byte[8192];
			while (true)
			{
				int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
				if (read == 0) break;
				hasher.Append(new ReadOnlySpan<byte>(buffer, 0, read));
			}
			return Convert.ToHexString(hasher.GetCurrentHash());
		}

		// Minimal managed implementation of xxHash64 (public domain algorithm)
		private class XxHash64
		{
			private const ulong PRIME1 = 11400714785074694791UL;
			private const ulong PRIME2 = 14029467366897019727UL;
			private const ulong PRIME3 = 1609587929392839161UL;
			private const ulong PRIME4 = 9650029242287828579UL;
			private const ulong PRIME5 = 2870177450012600261UL;

			private ulong _v1, _v2, _v3, _v4;
			private ulong _totalLen;
			private byte[] _buffer = new byte[32];
			private int _bufferLen = 0;

			public XxHash64()
			{
				_v1 = unchecked(PRIME1 + PRIME2);
				_v2 = PRIME2;
				_v3 = 0UL;
				_v4 = unchecked(ulong.MaxValue - PRIME1);
				_totalLen = 0;
			}

			public void Append(ReadOnlySpan<byte> input)
			{
				_totalLen += (ulong)input.Length;

				int offset = 0;
				if (_bufferLen + input.Length < 32)
				{
					input.CopyTo(_buffer.AsSpan(_bufferLen));
					_bufferLen += input.Length;
					return;
				}

				if (_bufferLen > 0)
				{
					int need = 32 - _bufferLen;
					input.Slice(0, need).CopyTo(_buffer.AsSpan(_bufferLen));
					ProcessChunk(_buffer.AsSpan(0, 32));
					offset += need;
					_bufferLen = 0;
				}

				while (offset + 32 <= input.Length)
				{
					ProcessChunk(input.Slice(offset, 32));
					offset += 32;
				}

				if (offset < input.Length)
				{
					var remaining = input.Slice(offset);
					remaining.CopyTo(_buffer);
					_bufferLen = remaining.Length;
				}
			}

			private static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));

			private void ProcessChunk(ReadOnlySpan<byte> chunk)
			{
				ulong k1 = BitConverter.ToUInt64(chunk.Slice(0, 8));
				_v1 = RotateLeft(_v1 + k1 * PRIME2, 31) * PRIME1;
				ulong k2 = BitConverter.ToUInt64(chunk.Slice(8, 8));
				_v2 = RotateLeft(_v2 + k2 * PRIME2, 31) * PRIME1;
				ulong k3 = BitConverter.ToUInt64(chunk.Slice(16, 8));
				_v3 = RotateLeft(_v3 + k3 * PRIME2, 31) * PRIME1;
				ulong k4 = BitConverter.ToUInt64(chunk.Slice(24, 8));
				_v4 = RotateLeft(_v4 + k4 * PRIME2, 31) * PRIME1;
			}

			public byte[] GetCurrentHash()
			{
				ulong h64;
				if (_totalLen >= 32)
				{
					h64 = RotateLeft(_v1, 1) + RotateLeft(_v2, 7) + RotateLeft(_v3, 12) + RotateLeft(_v4, 18);
					h64 = Mix64(h64, _v1);
					h64 = Mix64(h64, _v2);
					h64 = Mix64(h64, _v3);
					h64 = Mix64(h64, _v4);
				}
				else
				{
					h64 = PRIME5;
				}

				h64 += _totalLen;

				int idx = 0;
				while (idx + 8 <= _bufferLen)
				{
					ulong k1 = BitConverter.ToUInt64(_buffer, idx);
					h64 = RotateLeft(h64 ^ (k1 * PRIME2), 31) * PRIME1 + PRIME4;
					idx += 8;
				}

				while (idx < _bufferLen)
				{
					h64 = RotateLeft(h64 ^ ((_buffer[idx]) * PRIME5), 11) * PRIME1;
					idx++;
				}

				h64 ^= h64 >> 33;
				h64 *= PRIME2;
				h64 ^= h64 >> 29;
				h64 *= PRIME3;
				h64 ^= h64 >> 32;

				var outBytes = BitConverter.GetBytes(h64);
				return outBytes;
			}

			private static ulong Mix64(ulong h, ulong v)
			{
				v *= PRIME2;
				v = RotateLeft(v, 31);
				v *= PRIME1;
				h ^= v;
				h = h * PRIME1 + PRIME4;
				return h;
			}
		}






	}
}
