using Pivot.Engine;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Pivot.Services
{
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
            // Engine側で初期化するので何もしない
            return Task.CompletedTask;
        }

        public async Task<string?> GetOrCreateThumbnailAsync(string sourcePath, int width, int height, CancellationToken ct = default)
        {
            // すべてEngineに丸投げします
            return await _engine.GetThumbnailAsync(sourcePath, width, height, ct);
        }

        public string? TryGetCachedThumbnailPath(string sourcePath, int width, int height)
        {
            // キャッシュ確認もEngineに任せるか、一旦nullでOK
            return null;
        }

        public void Dispose() { }
    }
}
