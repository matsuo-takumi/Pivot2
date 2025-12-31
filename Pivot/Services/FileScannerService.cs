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
		private readonly Pivot.Repositories.IAssetRepository _repository;
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
		private readonly ConcurrentQueue<ItemChangeData<AssetEntity>> _assetChangeBuffer = new();
		private System.Timers.Timer? _assetFlushTimer;
		private const int AssetFlushIntervalMs = 300; // Phase 6: move to config
		private const int AssetFlushBatchMax = 100;   // Phase 6: move to config

		public FileScannerService(ILogger<FileScannerService> logger, Pivot.Repositories.IAssetRepository repository, IMessenger messenger, IConfiguration? configuration = null, ICatalogService? catalogService = null, IThumbnailService? thumbnailService = null)
		{
			_logger = logger;
			_repository = repository;
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
						await ProcessFileChangeAsync(path, ct);
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

		
	// ===== NEW SIMPLIFIED FILE PROCESSING =====
	
	/// <summary>
	/// Simplified file processing for initial scan (no watcher events)
	/// </summary>
	private async Task ProcessFileChangeAsync(string path, CancellationToken ct)
	{
		try
		{
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
			var existing = await _repository.GetByPathAsync(path, ct);
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
					hash = await ComputeXxHash64Async(path, ct);
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
						thumbnailPath = await _thumbnailService.GetOrCreateThumbnailAsync(path, 300, 200);
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

			await _repository.UpsertAsync(asset, ct);

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
	/// Old ProcessFileChange for watcher events - needs rewrite
	/// </summary>
	private async Task ProcessFileChange(string path, WatcherChangeTypes changeType, string? newPath, CancellationToken ct)
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
			try { _assetFlushTimer?.Dispose(); } catch { }
			GC.SuppressFinalize(this);
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
