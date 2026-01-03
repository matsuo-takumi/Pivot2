namespace Pivot.Utilities
{
    /// <summary>
    /// Helper class for getting the best image path (thumbnail or original).
    /// </summary>
    public static class ImagePathHelper
    {
        /// <summary>
        /// Returns ThumbnailPath if available, otherwise falls back to FilePath.
        /// </summary>
        public static string GetImageSource(string? thumbnailPath, string? filePath)
        {
            if (!string.IsNullOrEmpty(thumbnailPath))
            {
                return thumbnailPath;
            }
            return filePath ?? string.Empty;
        }
    }
}
