using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Pivot.Messages;
using Pivot.Models;
using SixLabors.ImageSharp;

namespace Pivot.Services
{
    /// <summary>
    /// アセットのバックグラウンドインデクサー。
    /// ファイルシステムとデータベースの同期、メタデータ抽出を行います。
    /// </summary>
    public class AssetIndexerService : IDisposable
    {
        private readonly IAssetRepository _repository;
        private readonly ILogger<AssetIndexerService> _logger;
        private readonly IMessenger _messenger;
        private readonly IThumbnailService? _thumbnailService;

        private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
        private readonly SemaphoreSlim _syncLock = new(1, 1);
        private CancellationTokenSource? _indexCts;

        // 対応する画像拡張子
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga", ".tif", ".tiff", ".webp", ".ico"
        };

        // 対応する3Dモデル拡張子
        private static readonly HashSet<string> ModelExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".obj", ".fbx", ".gltf", ".glb", ".dae", ".3ds", ".blend", ".ma", ".mb", ".usd", ".usdz", ".ply", ".stl"
        };

        // 対応する動画拡張子
        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".avi", ".mkv", ".webm", ".wmv"
        };

        public AssetIndexerService(
            IAssetRepository repository,
            ILogger<AssetIndexerService> logger,
            IMessenger messenger,
            IThumbnailService? thumbnailService = null)
        {
            _repository = repository;
            _logger = logger;
            _messenger = messenger;
            _thumbnailService = thumbnailService;
        }

        /// <summary>
        /// 指定ディレクトリの差分同期を実行
        /// </summary>
        public async Task SyncDirectoriesAsync(
            IEnumerable<string> directories,
            CancellationToken ct = default)
        {
            await _syncLock.WaitAsync(ct);
            try
            {
                var dirList = directories.Where(d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d)).ToList();
                if (dirList.Count == 0) return;

                _logger.LogInformation("AssetIndexer: Starting sync for {Count} directories", dirList.Count);

                // 1. DBから既存ファイルマップ取得
                var dbFiles = await _repository.GetFileMapAsync(dirList, ct);
                _logger.LogDebug("AssetIndexer: Found {Count} existing assets in DB", dbFiles.Count);

                // 2. ファイルシステムをスキャン
                var toAdd = new List<AssetFile>();
                var toUpdate = new List<AssetFile>();
                var scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var dir in dirList)
                {
                    try
                    {
                        foreach (var file in EnumerateAssetFiles(dir))
                        {
                            ct.ThrowIfCancellationRequested();
                            scannedPaths.Add(file.FullName);

                            if (!dbFiles.TryGetValue(file.FullName, out var dbTicks))
                            {
                                // 新規ファイル
                                var asset = CreateAssetFromFileInfo(file);
                                toAdd.Add(asset);
                            }
                            else if (dbTicks != file.LastWriteTimeUtc.Ticks)
                            {
                                // 更新されたファイル
                                var existing = await _repository.GetByPathAsync(file.FullName, ct);
                                if (existing != null)
                                {
                                    UpdateAssetFromFileInfo(existing, file);
                                    toUpdate.Add(existing);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "AssetIndexer: Error scanning directory {Dir}", dir);
                    }
                }

                // 3. 削除されたファイルを検出
                var toDelete = dbFiles.Keys
                    .Where(path => !scannedPaths.Contains(path))
                    .ToList();

                // 4. バッチ更新
                if (toAdd.Count > 0)
                {
                    await _repository.AddRangeAsync(toAdd, ct);
                    _logger.LogInformation("AssetIndexer: Added {Count} new assets", toAdd.Count);
                }

                if (toUpdate.Count > 0)
                {
                    await _repository.UpdateRangeAsync(toUpdate, ct);
                    _logger.LogInformation("AssetIndexer: Updated {Count} assets", toUpdate.Count);
                }

                if (toDelete.Count > 0)
                {
                    await _repository.DeleteRangeAsync(toDelete, ct);
                    _logger.LogInformation("AssetIndexer: Deleted {Count} assets", toDelete.Count);
                }

                // 5. UI通知
                if (toAdd.Count > 0 || toUpdate.Count > 0 || toDelete.Count > 0)
                {
                    _messenger.Send(new AssetsSyncedMessage(toAdd.Count, toUpdate.Count, toDelete.Count, true));
                }

                _logger.LogInformation("AssetIndexer: Sync complete. Added={Add}, Updated={Upd}, Deleted={Del}",
                    toAdd.Count, toUpdate.Count, toDelete.Count);
            }
            finally
            {
                _syncLock.Release();
            }
        }

        /// <summary>
        /// 未インデックスのアセットのメタデータを抽出（バックグラウンド処理）
        /// </summary>
        public async Task ExtractMetadataAsync(CancellationToken ct = default)
        {
            try
            {
                _indexCts?.Cancel();
            }
            catch { }
            _indexCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = _indexCts.Token;

            while (!token.IsCancellationRequested)
            {
                var unindexed = await _repository.GetUnindexedAssetsAsync(50, token);
                if (unindexed.Count == 0)
                {
                    // すべてインデックス済み
                    await Task.Delay(5000, token); // 5秒後に再チェック
                    continue;
                }

                foreach (var asset in unindexed)
                {
                    if (token.IsCancellationRequested) break;

                    try
                    {
                        await ExtractAssetMetadataAsync(asset, token);
                        asset.IsIndexed = true;
                        asset.IndexedAt = DateTime.UtcNow;
                        await _repository.UpdateAsync(asset, token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "AssetIndexer: Failed to extract metadata for {Path}", asset.FilePath);
                        // マークしてスキップ
                        asset.IsIndexed = true;
                        await _repository.UpdateAsync(asset, token);
                    }
                }

                // 通知
                _messenger.Send(new AssetsSyncedMessage(0, unindexed.Count, 0));
            }
        }

        /// <summary>
        /// FileSystemWatcherを開始
        /// </summary>
        public void StartWatching(IEnumerable<string> directories)
        {
            StopWatching();

            foreach (var dir in directories.Where(d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d)))
            {
                try
                {
                    var watcher = new FileSystemWatcher(dir)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                        EnableRaisingEvents = true
                    };

                    watcher.Created += OnFileCreated;
                    watcher.Changed += OnFileChanged;
                    watcher.Deleted += OnFileDeleted;
                    watcher.Renamed += OnFileRenamed;

                    _watchers[dir] = watcher;
                    _logger.LogInformation("AssetIndexer: Started watching {Dir}", dir);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AssetIndexer: Failed to start watcher for {Dir}", dir);
                }
            }
        }

        /// <summary>
        /// FileSystemWatcherを停止
        /// </summary>
        public void StopWatching()
        {
            foreach (var watcher in _watchers.Values)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch { }
            }
            _watchers.Clear();
        }

        // =============== Private Methods ===============

        private IEnumerable<FileInfo> EnumerateAssetFiles(string directory)
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
            };

            foreach (var file in new DirectoryInfo(directory).EnumerateFiles("*", options))
            {
                var ext = file.Extension.ToLowerInvariant();
                if (ImageExtensions.Contains(ext) || ModelExtensions.Contains(ext) || VideoExtensions.Contains(ext))
                {
                    yield return file;
                }
            }
        }

        private AssetFile CreateAssetFromFileInfo(FileInfo file)
        {
            return new AssetFile
            {
                FilePath = file.FullName,
                Directory = file.DirectoryName ?? string.Empty,
                FileName = file.Name,
                Extension = file.Extension.ToLowerInvariant(),
                Kind = DetermineAssetKind(file.Extension),
                FileSize = file.Length,
                LastModifiedTicks = file.LastWriteTimeUtc.Ticks,
                IsIndexed = false
            };
        }

        private void UpdateAssetFromFileInfo(AssetFile asset, FileInfo file)
        {
            asset.FileSize = file.Length;
            asset.LastModifiedTicks = file.LastWriteTimeUtc.Ticks;
            asset.IsIndexed = false; // 再インデックス必要
        }

        private AssetKind DetermineAssetKind(string extension)
        {
            var ext = extension.ToLowerInvariant();
            if (ImageExtensions.Contains(ext)) return AssetKind.Image;
            if (ModelExtensions.Contains(ext)) return AssetKind.Model;
            if (VideoExtensions.Contains(ext)) return AssetKind.Video;
            return AssetKind.Other;
        }

        private async Task ExtractAssetMetadataAsync(AssetFile asset, CancellationToken ct)
        {
            if (asset.Kind == AssetKind.Image && File.Exists(asset.FilePath))
            {
                try
                {
                    // ImageSharpでヘッダーのみ読み取り（超高速）
                    var info = await Image.IdentifyAsync(asset.FilePath, ct);
                    if (info != null)
                    {
                        asset.Width = info.Width;
                        asset.Height = info.Height;
                        asset.AspectRatio = info.Height > 0 ? (double)info.Width / info.Height : 1.0;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "AssetIndexer: Could not read image dimensions for {Path}", asset.FilePath);
                }
            }
        }

        // =============== FileSystemWatcher Events ===============

        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                var ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
                if (!IsAssetExtension(ext)) return;

                await Task.Delay(500); // ファイル書き込み完了を待つ

                var file = new FileInfo(e.FullPath);
                if (!file.Exists) return;

                var asset = CreateAssetFromFileInfo(file);
                await _repository.AddAsync(asset);
                _messenger.Send(new AssetFileChangedMessage(asset, e.FullPath, AssetFileChangedMessage.ChangeType.Added));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AssetIndexer: Error handling file created: {Path}", e.FullPath);
            }
        }

        private async void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                var ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
                if (!IsAssetExtension(ext)) return;

                await Task.Delay(500);

                var file = new FileInfo(e.FullPath);
                if (!file.Exists) return;

                var existing = await _repository.GetByPathAsync(e.FullPath);
                if (existing != null)
                {
                    UpdateAssetFromFileInfo(existing, file);
                    await _repository.UpdateAsync(existing);
                    _messenger.Send(new AssetFileChangedMessage(existing, e.FullPath, AssetFileChangedMessage.ChangeType.Updated));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AssetIndexer: Error handling file changed: {Path}", e.FullPath);
            }
        }

        private async void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            try
            {
                await _repository.DeleteAsync(e.FullPath);
                _messenger.Send(new AssetFileChangedMessage(null, e.FullPath, AssetFileChangedMessage.ChangeType.Deleted));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AssetIndexer: Error handling file deleted: {Path}", e.FullPath);
            }
        }

        private async void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                // 旧パス削除
                await _repository.DeleteAsync(e.OldFullPath);

                // 新パス追加
                var ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
                if (IsAssetExtension(ext))
                {
                    var file = new FileInfo(e.FullPath);
                    if (file.Exists)
                    {
                        var asset = CreateAssetFromFileInfo(file);
                        await _repository.AddAsync(asset);
                        _messenger.Send(new AssetFileChangedMessage(asset, e.FullPath, AssetFileChangedMessage.ChangeType.Added));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AssetIndexer: Error handling file renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
            }
        }

        private bool IsAssetExtension(string ext)
        {
            return ImageExtensions.Contains(ext) || ModelExtensions.Contains(ext) || VideoExtensions.Contains(ext);
        }

        public void Dispose()
        {
            StopWatching();
            try { _indexCts?.Cancel(); } catch { }
            try { _indexCts?.Dispose(); } catch { }
            _syncLock.Dispose();
        }
    }
}
