using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Pivot.Utilities
{
    /// <summary>
    /// Memory-efficient cache for BitmapImage objects using WeakReference.
    /// Allows garbage collection when memory pressure is high while maintaining performance.
    /// </summary>
    public sealed class WeakBitmapCache
    {
        private readonly ConcurrentDictionary<string, WeakReference<BitmapImage>> _cache = new();
        private readonly Timer _cleanupTimer;
        private const int CleanupIntervalMs = 30000; // 30 seconds

        public WeakBitmapCache()
        {
            // Periodic cleanup of dead references
            _cleanupTimer = new Timer(CleanupDeadReferences, null, CleanupIntervalMs, CleanupIntervalMs);
        }

        /// <summary>
        /// Try to get a cached BitmapImage. Returns null if not cached or collected.
        /// </summary>
        public BitmapImage? TryGet(string key)
        {
            if (_cache.TryGetValue(key, out var weakRef))
            {
                if (weakRef.TryGetTarget(out var bitmap))
                {
                    return bitmap;
                }
                else
                {
                    // Reference was collected, remove from cache
                    _cache.TryRemove(key, out _);
                }
            }
            return null;
        }

        /// <summary>
        /// Add or update a BitmapImage in the cache using WeakReference.
        /// </summary>
        public void Set(string key, BitmapImage bitmap)
        {
            _cache[key] = new WeakReference<BitmapImage>(bitmap);
        }

        /// <summary>
        /// Remove dead references from the cache to prevent memory bloat.
        /// </summary>
        private void CleanupDeadReferences(object? state)
        {
            try
            {
                var deadKeys = new System.Collections.Generic.List<string>();
                
                foreach (var kvp in _cache)
                {
                    if (!kvp.Value.TryGetTarget(out _))
                    {
                        deadKeys.Add(kvp.Key);
                    }
                }

                foreach (var key in deadKeys)
                {
                    _cache.TryRemove(key, out _);
                }

                // Log cleanup results (optional)
                if (deadKeys.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[WeakBitmapCache] Cleaned up {deadKeys.Count} dead references");
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        /// <summary>
        /// Clear all cached items.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Get current cache count (including dead references).
        /// </summary>
        public int Count => _cache.Count;

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _cache.Clear();
        }
    }
}
