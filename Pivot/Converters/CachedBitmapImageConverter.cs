using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using Pivot.Utilities;

namespace Pivot.Converters
{
    /// <summary>
    /// Cached version of BitmapImageDecodeConverter that reuses BitmapImage instances
    /// to prevent flickering during filtering and sorting operations.
    /// </summary>
    public sealed class CachedBitmapImageConverter : IValueConverter
    {
        // Shared cache across all converter instances
        private static readonly WeakBitmapCache _cache = new();

        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            var path = value as string;
            if (string.IsNullOrWhiteSpace(path)) return null;

            try
            {
                // Parse decode size parameter
                int decodeSize = 300; // Default
                if (parameter != null && int.TryParse(parameter.ToString(), out var px) && px > 0)
                {
                    decodeSize = px;
                }

                // Create cache key based on path and size
                var cacheKey = $"{path}|{decodeSize}";

                // Try to get from cache first
                var cached = _cache.TryGet(cacheKey);
                if (cached != null)
                {
                    return cached;
                }

                // Create new BitmapImage if not cached
                var uri = new Uri(path, UriKind.RelativeOrAbsolute);
                var bmp = new BitmapImage();

                // Set decode pixel width for memory optimization
                bmp.DecodePixelWidth = decodeSize;

                // Optimization settings
                bmp.CreateOptions = BitmapCreateOptions.None;
                
                // Set URI source
                bmp.UriSource = uri;

                // Cache the newly created bitmap
                _cache.Set(cacheKey, bmp);

                return bmp;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CachedBitmapImageConverter] Error loading image {path}: {ex.Message}");
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // OneWay binding only - ConvertBack not used
            return null!;
        }

        /// <summary>
        /// Clear the entire cache (useful for memory management).
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Get current cache statistics.
        /// </summary>
        public static int GetCacheCount()
        {
            return _cache.Count;
        }
    }
}
