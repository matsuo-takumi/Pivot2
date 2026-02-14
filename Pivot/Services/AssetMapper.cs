using System;
using Pivot.Engine.Models;
using Pivot.Models;

namespace Pivot.Services
{
    /// <summary>
    /// Centralized mapper for converting between AssetEntity (DB) and TemplateItem (UI).
    /// This eliminates duplicate conversion logic scattered across the codebase.
    /// </summary>
    /// <summary>
    /// Centralized mapper for converting between AssetEntity (DB) and UI Models.
    /// </summary>
    public static class AssetMapper
    {
        public static AssetModel ToAssetModel(AssetEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return new AssetModel(entity);
        }

        /// <summary>
        /// Deprecated: Use ToAssetModel instead. Kept for legacy compatibility if any.
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

        public static TemplateItem ToPreviewItem(AssetEntity entity) => ToTemplateItem(entity);

        // ... other methods if needed, but we are moving to AssetModel
    }
}
