namespace Pivot.Engine;

public interface IPivotEngine
{
    Task InitializeAsync(string cacheDirectory, string dbPath);
    
    Task<string> GetThumbnailAsync(string assetId, int width, int height, CancellationToken ct = default);
    
    Task<Models.AssetEntity?> GetMetadataAsync(string assetId, CancellationToken ct = default);
}
