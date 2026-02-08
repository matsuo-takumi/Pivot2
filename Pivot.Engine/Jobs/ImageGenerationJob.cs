using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using Pivot.Engine.Core;
using Pivot.Engine.Data;
using Microsoft.EntityFrameworkCore;

namespace Pivot.Engine.Jobs;

public class ImageGenerationJob : Job
{
    private readonly string _sourcePath;
    private readonly string _destinationPath;
    private readonly int _width;
    private readonly int _height;
    private readonly DbContextOptions<PivotDbContext> _dbContextOptions;

    public ImageGenerationJob(string id, string sourcePath, string destinationPath, int width, int height, DbContextOptions<PivotDbContext> dbContextOptions) 
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
            if (!File.Exists(_sourcePath)) return;
            
            // Allow some retries for file access (e.g. if file is being written)
            // For now, simple single attempt
            
            using var image = await Image.LoadAsync(_sourcePath, ct);
            
            var resizeOptions = new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(_width, _height),
                Sampler = (image.Width > 2000 || image.Height > 2000) 
                    ? KnownResamplers.Lanczos3 
                    : KnownResamplers.Bicubic,
                Compand = false
            };
            
            image.Mutate(x => x.Resize(resizeOptions));
            
            Directory.CreateDirectory(Path.GetDirectoryName(_destinationPath)!);
            
            var encoder = new PngEncoder
            {
                CompressionLevel = PngCompressionLevel.BestCompression
            };

            await image.SaveAsPngAsync(_destinationPath, encoder, ct);
            
            // Update DB
            using var db = new PivotDbContext(_dbContextOptions);
            var metadata = await db.AssetMetadata.FirstOrDefaultAsync(a => a.FilePath == _sourcePath, ct);
            if (metadata != null)
            {
                metadata.ThumbnailPath = _destinationPath;
                await db.SaveChangesAsync(ct);
            }
            else
            {
                // Create new entry
                db.AssetMetadata.Add(new AssetMetadata
                {
                    FilePath = _sourcePath,
                    ThumbnailPath = _destinationPath,
                    LastModifiedTicks = File.GetLastWriteTimeUtc(_sourcePath).Ticks,
                    FileSizeBytes = new FileInfo(_sourcePath).Length
                });
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception)
        {
            // Log?
        }
    }
}
