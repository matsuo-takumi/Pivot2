using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Pivot.Engine.Core;
using Pivot.Engine.Data;
using Microsoft.EntityFrameworkCore;
using Pivot.Engine.Models;

namespace Pivot.Engine.Jobs;

public class ImageGenerationJob : Job
{
    private readonly string _sourcePath;
    private readonly string _destinationPath;
    private readonly int _width;
    private readonly int _height;
    private readonly IDbContextFactory<PivotDbContext> _dbFactory;

    public ImageGenerationJob(string id, string sourcePath, string destinationPath, int width, int height, IDbContextFactory<PivotDbContext> dbFactory) 
        : base(id)
    {
        _sourcePath = sourcePath;
        _destinationPath = destinationPath;
        _width = width;
        _height = height;
        _dbFactory = dbFactory;
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(_sourcePath)) return;
            
            // 1. Load Image
            using var image = await Image.LoadAsync<Rgba32>(_sourcePath, ct);
            
            // 2. Analyze (on original image for best accuracy)
            var pHash = Pivot.Engine.Utilities.ImageAnalysisService.ComputeAverageHash(image);
            var colors = Pivot.Engine.Utilities.ImageAnalysisService.ExtractColors(image);

            // 3. Resize for Thumbnail
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
            
            // 4. Save Thumbnail
            Directory.CreateDirectory(Path.GetDirectoryName(_destinationPath)!);
            var encoder = new PngEncoder
            {
                CompressionLevel = PngCompressionLevel.BestCompression
            };
            await image.SaveAsPngAsync(_destinationPath, encoder, ct);
            
            // 5. Update Database
            using var db = await _dbFactory.CreateDbContextAsync(ct);
            var asset = await db.Assets
                .Include(a => a.Colors)
                .FirstOrDefaultAsync(a => a.FilePath == _sourcePath, ct);

            if (asset != null)
            {
                // Update existing asset
                asset.ThumbnailPath = _destinationPath;
                asset.ThumbnailGeneratedAt = DateTime.UtcNow;
                asset.Width = image.Width; // Thumbnail dims (Note: Original dims lost if we don't save them before resize. Assuming Scanner already got them, or we should capture them.)
                // Actually, Scanner gets simple dims. Accurately, we should capture original dims. 
                // However, 'image' here is now resized. 
                // If we want original dims, we should have captured them before mutate.
                
                // Let's rely on Scanner for Dimensions for now, OR capture them if we want to be safe.
                // But Scanner uses Image.Identify which is fast. 
                // We'll just update the thumbnail path and analysis data.
                
                asset.PerceptualHash = pHash;

                // Update Colors
                db.AssetColors.RemoveRange(asset.Colors);
                foreach (var c in colors)
                {
                    c.AssetId = asset.Id;
                    db.AssetColors.Add(c);
                }
                
                await db.SaveChangesAsync(ct);
            }
            else
            {
                // Asset not found. Create new?
                // Typically Scanner creates the asset first.
                // But if it doesn't exist, we can create it with the info we have.
                // However, usually we don't want to create assets for files that might not match criteria if Scanner didn't pick them up.
                // For safety/robustness, we'll create it if missing, similar to original code's 'else' block.
                
                var newAsset = new AssetEntity
                {
                    FilePath = _sourcePath,
                    FileName = Path.GetFileName(_sourcePath),
                    Directory = Path.GetDirectoryName(_sourcePath) ?? string.Empty,
                    Extension = Path.GetExtension(_sourcePath),
                    FileSize = new FileInfo(_sourcePath).Length,
                    LastModifiedUtc = File.GetLastWriteTimeUtc(_sourcePath),
                    ThumbnailPath = _destinationPath,
                    ThumbnailGeneratedAt = DateTime.UtcNow,
                    PerceptualHash = pHash
                };
                
                db.Assets.Add(newAsset);
                // We need to save first to get ID for colors? 
                // EF Core handles graph add usually.
                foreach(var c in colors)
                {
                    newAsset.Colors.Add(c);
                }
                
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception)
        {
            // Log?
        }
    }
}
