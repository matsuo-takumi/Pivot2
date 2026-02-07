using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Pivot.Services
{
    /// <summary>
    /// LRU cache for decoded BitmapImage objects to eliminate repeated decoding.
    /// Dramatically improves scrolling performance by keeping decoded images in memory.
    /// </summary>
    public interface IBitmapCacheService
    {
        Task<BitmapImage> GetOrLoadAsync(string imagePath, int decodeWidth, CancellationToken ct = default);
        void Clear();
    }

    public sealed class BitmapCacheService : IBitmapCacheService
    {
        private readonly LinkedList<CacheEntry> _lruList = new();
        private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly int _maxCacheSize;

        private class CacheEntry
        {
            public string Key { get; set; } = string.Empty;
            public BitmapImage Bitmap { get; set; } = null!;
        }

        public BitmapCacheService(int maxCacheSize = 100)
        {
            _maxCacheSize = maxCacheSize;
        }

        /// <summary>
        /// Get cached bitmap or load and decode asynchronously.
        /// Uses LRU eviction when cache is full.
        /// </summary>
        public async Task<BitmapImage> GetOrLoadAsync(string imagePath, int decodeWidth, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                return CreatePlaceholder();
            }

            var key = $"{imagePath}|{decodeWidth}";

            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Cache hit - move to front (most recently used)
                if (_cache.TryGetValue(key, out var node))
                {
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    return node.Value.Bitmap;
                }

                // Cache miss - load and decode
                var bitmap = await LoadAndDecodeAsync(imagePath, decodeWidth, ct).ConfigureAwait(false);

                // Add to cache
                var entry = new CacheEntry { Key = key, Bitmap = bitmap };
                var newNode = new LinkedListNode<CacheEntry>(entry);
                _lruList.AddFirst(newNode);
                _cache[key] = newNode;

                // Evict least recently used if cache is full
                if (_lruList.Count > _maxCacheSize)
                {
                    var lastNode = _lruList.Last;
                    if (lastNode != null)
                    {
                        _lruList.RemoveLast();
                        _cache.Remove(lastNode.Value.Key);
                    }
                }

                return bitmap;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Load image file and decode asynchronously on background thread.
        /// </summary>
        private async Task<BitmapImage> LoadAndDecodeAsync(string imagePath, int decodeWidth, CancellationToken ct)
        {
            try
            {
                // Load file on background thread
                var file = await StorageFile.GetFileFromPathAsync(imagePath).AsTask(ct).ConfigureAwait(false);
                
                // Open stream
                using var stream = await file.OpenReadAsync().AsTask(ct).ConfigureAwait(false);

                // Create BitmapImage with decoding options
                var bitmap = new BitmapImage();
                bitmap.DecodePixelWidth = decodeWidth;
                bitmap.DecodePixelType = DecodePixelType.Logical;

                // SetSourceAsync decodes on background thread
                await bitmap.SetSourceAsync(stream).AsTask(ct).ConfigureAwait(false);

                return bitmap;
            }
            catch
            {
                return CreatePlaceholder();
            }
        }

        /// <summary>
        /// Create a placeholder bitmap for failed loads.
        /// </summary>
        private BitmapImage CreatePlaceholder()
        {
            return new BitmapImage();
        }

        /// <summary>
        /// Clear all cached bitmaps.
        /// </summary>
        public void Clear()
        {
            _lock.Wait();
            try
            {
                _cache.Clear();
                _lruList.Clear();
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
