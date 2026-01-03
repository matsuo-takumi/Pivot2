namespace Pivot.Utilities
{
    /// <summary>
    /// Helper class for formatting file sizes.
    /// </summary>
    public static class FileSizeHelper
    {
        public static string FormatSize(long? bytes)
        {
            if (!bytes.HasValue || bytes.Value <= 0) return "-";

            string[] sizes = { "B", "KB", "MB", "GB" };
            double value = bytes.Value;
            int order = 0;

            while (value >= 1024 && order < sizes.Length - 1)
            {
                order++;
                value /= 1024;
            }

            return $"{value:0.#} {sizes[order]}";
        }
    }
}
