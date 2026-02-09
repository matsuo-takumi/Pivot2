using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Pivot.Engine.Models;
using System.Collections.Concurrent;

namespace Pivot.Engine.Utilities
{
    public class ImageAnalysisService
    {
        // 1. Perceptual Hash (Simplified Average Hash)
        // Returns 64-bit hash as ulong
        public static ulong ComputeAverageHash(Image<Rgba32> image)
        {
            // 1. Resize to 8x8
            using var clone = image.Clone(x => x
                .Resize(new ResizeOptions
                {
                    Size = new Size(8, 8),
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.Bicubic
                })
                .Grayscale());

            // 2. Calculate average
            long total = 0;
            var pixels = new byte[64];
            clone.ProcessPixelRows(accessor =>
            {
                int index = 0;
                for (int y = 0; y < 8; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < 8; x++)
                    {
                        var val = row[x].R; // R=G=B in grayscale
                        pixels[index++] = val;
                        total += val;
                    }
                }
            });

            int average = (int)(total / 64);

            // 3. Compute hash
            ulong hash = 0;
            for (int i = 0; i < 64; i++)
            {
                if (pixels[i] >= average)
                {
                    hash |= (1UL << (63 - i));
                }
            }

            return hash;
        }

        // 2. Color Extraction (Simplified K-Means / Quantization)
        // Returns top 5 colors
        public static List<AssetColor> ExtractColors(Image<Rgba32> image, int maxColors = 5)
        {
            // Resize for speed (e.g. 100x100 is enough for dominant colors)
            using var small = image.Clone(x => x.Resize(new ResizeOptions
            {
                Size = new Size(100, 100),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.NearestNeighbor
            }));

            // Simple histogram bucket approach (faster than strict K-Means)
            // Quantize to 5-bit per channel (32x32x32 buckets)
            var histogram = new Dictionary<int, int>();
            int totalPixels = 0;

            small.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < accessor.Width; x++)
                    {
                        var p = row[x];
                        if (p.A < 128) continue; // Skip transparent

                        int key = ((p.R >> 3) << 10) | ((p.G >> 3) << 5) | (p.B >> 3);
                        if (!histogram.ContainsKey(key)) histogram[key] = 0;
                        histogram[key]++;
                        totalPixels++;
                    }
                }
            });

            if (totalPixels == 0) return new List<AssetColor>();

            // Sort by count
            var topBuckets = histogram.OrderByDescending(x => x.Value).Take(maxColors).ToList();

            var result = new List<AssetColor>();
            foreach (var bucket in topBuckets)
            {
                // Unpack color (middle of the bucket)
                // key = (R >> 3) << 10 ...
                // R = (key >> 10) & 0x1F
                int r5 = (bucket.Key >> 10) & 0x1F;
                int g5 = (bucket.Key >> 5) & 0x1F;
                int b5 = bucket.Key & 0x1F;

                // Scale back to 8-bit (approx)
                byte r = (byte)((r5 << 3) | (r5 >> 2));
                byte g = (byte)((g5 << 3) | (g5 >> 2));
                byte b = (byte)((b5 << 3) | (b5 >> 2));

                result.Add(new AssetColor
                {
                    R = r,
                    G = g,
                    B = b,
                    Ratio = (float)bucket.Value / totalPixels
                });
            }

            return result;
        }
    }
}
