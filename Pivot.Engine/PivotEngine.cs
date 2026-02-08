using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Pivot.Engine.Core;
using Pivot.Engine.Data;

namespace Pivot.Engine;

public class PivotEngine : IPivotEngine, IDisposable
{
    private readonly ILogger<PivotEngine> _logger;
    private readonly JobScheduler _jobScheduler;
    private string _cacheDir = string.Empty;
    private string _dbPath = string.Empty;
    private PivotDbContext? _dbContext; // In real app, use IDbContextFactory or scope
    private bool _isInitialized;

    public PivotEngine(ILogger<PivotEngine> logger)
    {
        _logger = logger;
        _jobScheduler = new JobScheduler(Math.Max(2, Environment.ProcessorCount / 2));
    }

    public async Task InitializeAsync(string cacheDirectory, string dbPath)
    {
        _cacheDir = cacheDirectory;
        _dbPath = dbPath;
        Directory.CreateDirectory(_cacheDir);

        // Setup DB
        var optionsBuilder = new DbContextOptionsBuilder<PivotDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
        _dbContext = new PivotDbContext(optionsBuilder.Options);
        
        await _dbContext.Database.EnsureCreatedAsync();
        
        _isInitialized = true;
        _logger.LogInformation("PivotEngine initialized at {Path}", _cacheDir);
    }

    public async Task<Data.AssetMetadata?> GetMetadataAsync(string assetId, CancellationToken ct = default)
    {
        if (!_isInitialized || _dbContext == null) throw new InvalidOperationException("Engine not initialized");
        
        return await _dbContext.AssetMetadata
            .FirstOrDefaultAsync(a => a.FilePath == assetId, ct);
    }

    public async Task<string> GetThumbnailAsync(string assetId, int width, int height, CancellationToken ct = default)
    {
        if (!_isInitialized) throw new InvalidOperationException("Engine not initialized");

        // 1. Check DB for cached thumbnail
        // Use a new context for thread safety if this method is called concurrently (or rely on factory if injected)
        // Here we just create one for the read.
        using (var db = new PivotDbContext(new DbContextOptionsBuilder<PivotDbContext>().UseSqlite($"Data Source={_dbPath}").Options)) 
        {
            var meta = await db.AssetMetadata.FirstOrDefaultAsync(a => a.FilePath == assetId, ct);
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
        var dbOpts = new DbContextOptionsBuilder<PivotDbContext>().UseSqlite($"Data Source={_dbPath}").Options;
        
        if (IsImage(ext))
        {
            job = new Jobs.ImageGenerationJob(hash, assetId, thumbPath, width, height, dbOpts);
        }
        else if (IsVideo(ext))
        {
            job = new Jobs.VideoExtractionJob(hash, assetId, thumbPath, width, height, dbOpts);
        }
        else
        {
            job = new Jobs.ShellThumbnailJob(hash, assetId, thumbPath, width, height, dbOpts);
        }

        // Priority logic: simpler for now, always High if requested by UI? 
        // Or we can let UI pass priority. For now default to High as it's likely a visible item request.
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
    }
}
