using System;
using Pivot.Engine.Models;
using Pivot.Models;

namespace Pivot.Services
{
    /// <summary>
    /// Centralized mapper for converting between AssetEntity (DB) and TemplateItem (UI).
    /// This eliminates duplicate conversion logic scattered across the codebase.
    /// </summary>
    public static class AssetMapper
    {
        /// <summary>
        /// Convert AssetEntity to TemplateItem for UI display.
        /// </summary>
        public static TemplateItem ToTemplateItem(AssetEntity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return new TemplateItem
            {
                Kind = entity.Kind,
                Path = entity.FilePath,
                Name = entity.FileName,
                Size = entity.FileSize,
                LastModified = entity.LastModifiedUtc,
                AspectRatio = entity.AspectRatio ?? 1.0,
                PixelWidth = entity.Width ?? 0,
                PixelHeight = entity.Height ?? 0,
                ThumbnailPath = entity.ThumbnailPath ?? entity.FilePath,
                IsSelected = false
            };
        }

        /// <summary>
        /// Update existing TemplateItem from AssetEntity (for real-time updates).
        /// </summary>
        public static void UpdateTemplateItem(TemplateItem item, AssetEntity entity)
        {
            if (item == null || entity == null) return;

            item.Kind = entity.Kind;
            item.Path = entity.FilePath;
            item.Name = entity.FileName;
            item.Size = entity.FileSize;
            item.LastModified = entity.LastModifiedUtc;
            item.AspectRatio = entity.AspectRatio ?? 1.0;
            item.PixelWidth = entity.Width ?? 0;
            item.PixelHeight = entity.Height ?? 0;
            item.ThumbnailPath = entity.ThumbnailPath ?? entity.FilePath;
        }

        /// <summary>
        /// Create a lightweight TemplateItem for preview (minimal properties).
        /// </summary>
        public static TemplateItem ToPreviewItem(AssetEntity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return new TemplateItem
            {
                Path = entity.FilePath,
                Name = entity.FileName,
                ThumbnailPath = entity.ThumbnailPath ?? entity.FilePath
            };
        }
    }
}
