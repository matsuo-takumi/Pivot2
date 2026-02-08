using Pivot.Engine;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pivot.Services
{
    public interface IThumbnailService : IDisposable
    {
        Task InitializeAsync(string cacheDirectory, long maxCacheBytes);
        Task<string> GetOrCreateThumbnailAsync(string sourcePath, int width, int height, CancellationToken ct = default);
        string? TryGetCachedThumbnailPath(string sourcePath, int width, int height);
    }

    // UIとEngineをつなぐだけの薄いラッパー
    public class ThumbnailService : IThumbnailService
    {
        private readonly IPivotEngine _engine;
        private readonly ILogger<ThumbnailService> _logger;

        public ThumbnailService(IPivotEngine engine, ILogger<ThumbnailService> logger)
        {
            _engine = engine;
            _logger = logger;
        }

        public Task InitializeAsync(string cacheDirectory, long maxCacheBytes)
        {
            // Engine側で管理するため不要ですが、インターフェース互換のため残します
            return Task.CompletedTask;
        }

        public async Task<string> GetOrCreateThumbnailAsync(string sourcePath, int width, int height, CancellationToken ct = default)
        {
            // すべてEngineに丸投げします
            return await _engine.GetThumbnailAsync(sourcePath, width, height, ct);
        }

        public string? TryGetCachedThumbnailPath(string sourcePath, int width, int height)
        {
            // キャッシュ確認もEngineに任せるか、非同期取得を推奨するためnullを返します
            return null;
        }

        public void Dispose() { }
    }
}
