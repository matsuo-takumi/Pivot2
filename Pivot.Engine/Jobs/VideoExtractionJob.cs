using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Pivot.Engine.Core;
using Pivot.Engine.Data;
using Pivot.Engine.Models;

namespace Pivot.Engine.Jobs;

public class VideoExtractionJob : Job
{
    private readonly string _sourcePath;
    private readonly string _destinationPath;
    private readonly int _width;
    private readonly int _height;
    private readonly DbContextOptions<PivotDbContext> _dbContextOptions;

    public VideoExtractionJob(string id, string sourcePath, string destinationPath, int width, int height, DbContextOptions<PivotDbContext> dbContextOptions)
        : base(id)
    {
        _sourcePath = sourcePath;
        _destinationPath = destinationPath;
        _width = width;
        _height = height;
        _dbContextOptions = dbContextOptions;
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_destinationPath)!);

            // ffmpeg should be on PATH. We seek 1s into the video to avoid black frames at start.
            var ffmpeg = "ffmpeg";
            var args = $"-y -ss 00:00:01 -i \"{_sourcePath}\" -vframes 1 -vf \"scale='min({_width},iw)':'min({_height},ih)':force_original_aspect_ratio=decrease,pad={_width}:{_height}:(ow-iw)/2:(oh-ih)/2\" \"{_destinationPath}\"";

            var psi = new ProcessStartInfo(ffmpeg, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null) return;

            using (ct.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(true); } catch { }
            }))
            {
                await proc.WaitForExitAsync(ct);
            }

            if (proc.ExitCode == 0 && File.Exists(_destinationPath))
            {
                // Update DB
                using var db = new PivotDbContext(_dbContextOptions);
                var metadata = await db.Assets.FirstOrDefaultAsync(a => a.FilePath == _sourcePath, ct);
                if (metadata != null)
                {
                    metadata.ThumbnailPath = _destinationPath;
                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    db.Assets.Add(new AssetEntity
                    {
                        FilePath = _sourcePath,
                        FileName = Path.GetFileName(_sourcePath),
                        Directory = Path.GetDirectoryName(_sourcePath) ?? string.Empty,
                        Extension = Path.GetExtension(_sourcePath),
                        FileSize = new FileInfo(_sourcePath).Length,
                        LastModifiedUtc = File.GetLastWriteTimeUtc(_sourcePath),
                        ThumbnailPath = _destinationPath,
                        ThumbnailGeneratedAt = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync(ct);
                }
            }
        }
        catch (Exception)
        {
            // Log?
        }
    }
}
