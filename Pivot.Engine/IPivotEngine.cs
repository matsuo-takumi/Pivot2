namespace Pivot.Engine;

public interface IPivotEngine
{
    Task InitializeAsync(string cacheDirectory);
    
    Task<string> GetThumbnailAsync(string assetId, int width, int height, CancellationToken ct = default);
    
    Task<Models.AssetEntity?> GetMetadataAsync(string assetId, CancellationToken ct = default);

    Task ScanAsync(IEnumerable<string> rootPaths, IProgress<int>? progress = null, CancellationToken ct = default, HashSet<Models.AssetKind>? allowedKinds = null);
    
    Task ReconcileAsync(IEnumerable<string> validRootPaths, CancellationToken ct = default);
}

