using System;
using System.Collections.Generic;

namespace Pivot.Models
{
    public class AssetEntry
    {
        public int Id { get; set; }

        public string Path { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public long Size { get; set; }
        public string Hash { get; set; }
        public string TagsJson { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Thumbnail cache path (method 1: store file path reference)
        public string? ThumbnailCachePath { get; set; }
        
        // Image dimensions for aspect ratio calculation
        public int PixelWidth { get; set; }
        public int PixelHeight { get; set; }

        // 関連するプロジェクト（多対多はIDリストやブリッジテーブルで管理することを推奨）
        public List<ProjectEntry> Projects { get; set; } = new List<ProjectEntry>();

        // 関連するFileEntry（ある場合のみ）
        public FileEntry? File { get; set; }

        public AssetEntry()
        {
            Path = string.Empty;
            Name = string.Empty;
            Type = string.Empty;
            Hash = string.Empty;
            TagsJson = string.Empty;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            File = null;
        }
    }
}
