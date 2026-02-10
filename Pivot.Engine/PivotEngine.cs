using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Engine.Core;
using Pivot.Engine.Data;
using Pivot.Engine.Services;

namespace Pivot.Engine;

public class PivotEngine : IPivotEngine, IDisposable
{
    private readonly ILogger<PivotEngine> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IMessenger _messenger;
    private readonly IDbContextFactory<PivotDbContext> _dbFactory;
    private readonly JobScheduler _jobScheduler;
    private string _cacheDir = string.Empty;
    private PivotDbContext? _dbContext; // For metadata retrieval
    private readonly FileScannerService _fileScanner;
    private bool _isInitialized;

    public PivotEngine(
        ILogger<PivotEngine> logger, 
        ILoggerFactory loggerFactory, 
        IMessenger messenger, 
        IDbContextFactory<PivotDbContext> dbFactory,
        FileScannerService fileScanner)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _messenger = messenger;
        _dbFactory = dbFactory;
        _fileScanner = fileScanner;
        _jobScheduler = new JobScheduler(Math.Max(2, Environment.ProcessorCount / 2));
    }

    public async Task InitializeAsync(string cacheDirectory)
    {
        _cacheDir = cacheDirectory;
        Directory.CreateDirectory(_cacheDir);

        // Initialize DB (Recreate if missing)
        using (var db = await _dbFactory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
        }

        // Initialize main context
        _dbContext = await _dbFactory.CreateDbContextAsync();
        
        // Initialize FileScannerService delegate to resolve circular dependency
        _fileScanner.ThumbnailGenerator = (path, w, h, ct) => GetThumbnailAsync(path, w, h, ct);

        _isInitialized = true;
        _logger.LogInformation("PivotEngine initialized at {Path}", _cacheDir);
    }

    public async Task<Models.AssetEntity?> GetMetadataAsync(string assetId, CancellationToken ct = default)
    {
        if (!_isInitialized || _dbContext == null) throw new InvalidOperationException("Engine not initialized");
        
        return await _dbContext.Assets
            .FirstOrDefaultAsync(a => a.FilePath == assetId, ct);
    }

    public async Task ScanAsync(IEnumerable<string> rootPaths, IProgress<int>? progress = null, CancellationToken ct = default, HashSet<Models.AssetKind>? allowedKinds = null)
    {
        if (!_isInitialized || _fileScanner == null) throw new InvalidOperationException("Engine not initialized");
        await _fileScanner.ScanAsync(rootPaths, progress, ct, allowedKinds);
    }

    public async Task ReconcileAsync(IEnumerable<string> validRootPaths, CancellationToken ct = default)
    {
        if (!_isInitialized || _fileScanner == null) throw new InvalidOperationException("Engine not initialized");
        await _fileScanner.ReconcileAsync(validRootPaths, ct);
    }


    public async Task<string> GetThumbnailAsync(string assetId, int width, int height, CancellationToken ct = default)
    {
        if (!_isInitialized) throw new InvalidOperationException("Engine not initialized");

        // 1. Check DB for cached thumbnail
        using (var db = await _dbFactory.CreateDbContextAsync(ct)) 
        {
            var meta = await db.Assets.FirstOrDefaultAsync(a => a.FilePath == assetId, ct);
            if (meta != null && !string.IsNullOrEmpty(meta.ThumbnailPath) && File.Exists(meta.ThumbnailPath))
            {
                return meta.ThumbnailPath;
            }
        }

        // 2. Queue job if not found
        // Determine cache path
        var ext = Path.GetExtension(assetId).ToLowerInvariant();
        var hash = ComputeHash(assetId, File.GetLastWriteTimeUtc(assetId).Ticks, new FileInfo(assetId).Length, width, height);
        var thumbPath = Path.Combine(_cacheDir, hash + ".png");

        // Double check filesystem (maybe DB is out of sync or not yet updated)
        if (File.Exists(thumbPath)) return thumbPath;

        Job job;
        
        if (IsImage(ext))
        {
            job = new Jobs.ImageGenerationJob(hash, assetId, thumbPath, width, height, _dbFactory);
        }
        else if (IsVideo(ext))
        {
            job = new Jobs.VideoExtractionJob(hash, assetId, thumbPath, width, height, _dbFactory);
        }
        else
        {
            job = new Jobs.ShellThumbnailJob(hash, assetId, thumbPath, width, height, _dbFactory);
        }

        _jobScheduler.Enqueue(job, JobPriority.High);
        
        return string.Empty; // Return empty to indicate "processing"
    }

    private static bool IsImage(string ext)
    {
        return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".webp";
    }

    private static bool IsVideo(string ext)
    {
        return ext == ".mp4" || ext == ".mov" || ext == ".avi" || ext == ".mkv" || ext == ".webm";
    }

    private static string ComputeHash(string path, long ticks, long size, int w, int h)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var raw = System.Text.Encoding.UTF8.GetBytes(path + "|" + ticks + "|" + size + "|" + w + "x" + h);
        var hash = sha.ComputeHash(raw);
        var sb = new System.Text.StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    public void Dispose()
    {
        _jobScheduler.Dispose();
        _dbContext?.Dispose();
        _fileScanner?.Dispose();
    }
}
