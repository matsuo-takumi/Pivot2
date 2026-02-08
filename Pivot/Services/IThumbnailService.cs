using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pivot.Services
{
    public interface IThumbnailService : IDisposable
    {
        Task InitializeAsync(string cacheDirectory, long maxCacheBytes);
        Task<string?> GetOrCreateThumbnailAsync(string sourcePath, int width, int height, CancellationToken ct = default);
        string? TryGetCachedThumbnailPath(string sourcePath, int width, int height);
    }
}
