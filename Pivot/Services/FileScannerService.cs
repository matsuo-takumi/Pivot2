using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.IO;
using System;
using System.Security.Cryptography;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Pivot.Services
{
    public class FileScannerService : IDisposable
    {
        private readonly ILogger<FileScannerService> _logger;
        private readonly MetadataService _metadataService;
        private FileSystemWatcher? _watcher;
        private string? _currentRootPath;

        // 短期間の複数イベントをまとめるためのディレイとキュー
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _delayTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        private const int EventDelayMs = 500;

        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga", ".tif", ".tiff", ".webp"
        };

        public FileScannerService(ILogger<FileScannerService> logger, MetadataService metadataService)
        {
            _logger = logger;
            _metadataService = metadataService;
        }

        public async Task ScanAsync(IEnumerable<string> rootPaths, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            StopWatching(); // 既存のWatcherを停止

            foreach (var rootPath in rootPaths)
            {
                if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                {
                    _logger.LogWarning("Root path invalid: {rootPath}", rootPath);
                    continue;
                }
                _currentRootPath = rootPath; // 現在のスキャン対象パスを保持

                var files = Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories);
                int total = 0;
                try
                {
                    total = 0;
                    foreach (var _ in files) total++;
                }
                catch { /* 省略 */ }

                int processed = 0;
                foreach (var path in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await ProcessFileChange(path, WatcherChangeTypes.Created, null, cancellationToken); // ファイル作成として処理
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process file during initial scan: {path}", path);
                    }
                    finally
                    {
                        processed++;
                        if (total > 0)
                        {
                            int percent = (int)Math.Clamp(processed * 100.0 / total, 0, 100);
                            progress?.Report(percent);
                        }
                    }
                }

                progress?.Report(100);
                _logger.LogInformation("Scan completed for {rootPath}", rootPath);
                StartWatching(rootPath); // スキャン後に監視を開始
            }
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
            }
        }

        private async void OnCreated(object sender, FileSystemEventArgs e) => await DelayAndProcessFile(e.FullPath, e.ChangeType);
        private async void OnChanged(object sender, FileSystemEventArgs e) => await DelayAndProcessFile(e.FullPath, e.ChangeType);
        private async void OnDeleted(object sender, FileSystemEventArgs e) => await DelayAndProcessFile(e.FullPath, e.ChangeType);
        private async void OnRenamed(object sender, RenamedEventArgs e) => await DelayAndProcessFile(e.OldFullPath, e.ChangeType, e.FullPath);

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

        private async Task ProcessFileChange(string path, WatcherChangeTypes changeType, string? newPath = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("File system event: {ChangeType} - {Path}", changeType, path);

                if (changeType == WatcherChangeTypes.Deleted)
                {
                    await _metadataService.DeleteFileAsync(path);
                    _logger.LogInformation("Deleted metadata for: {Path}", path);
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
                    string hash = await ComputeMD5Async(path, cancellationToken);
                    await _metadataService.UpsertFileAsync(path, type, fileInfo.Length, fileInfo.LastWriteTimeUtc, hash);
                    _logger.LogInformation("Upserted metadata for: {Path}", path);
                }
            }
            catch (OperationCanceledException) { /* スキャンキャンセル */ }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file system event: {Path}", path);
            }
        }

        public void Dispose()
        {
            StopWatching();
            foreach (var cts in _delayTokens.Values) cts.Dispose();
            _delayTokens.Clear();
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
    }
}
