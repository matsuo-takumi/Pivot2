using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Pivot.Models
{
    [Index(nameof(FilePath), IsUnique = true)]
    [Index(nameof(Directory))]
    [Index(nameof(Kind))]
    [Index(nameof(Hash))]
    [Index(nameof(LastModifiedUtc))]
    // Performance indexes for scalable browser
    [Index(nameof(Kind), nameof(LastModifiedUtc), Name = "IX_Asset_Kind_Date")]
    [Index(nameof(AspectRatio), Name = "IX_Asset_AspectRatio")]
    [Index(nameof(Rating), Name = "IX_Asset_Rating")]
    public class AssetEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(4096)]
        public string FilePath { get; set; } = string.Empty;

        [MaxLength(512)]
        public string FileName { get; set; } = string.Empty;
        
        [MaxLength(4096)]
        public string Directory { get; set; } = string.Empty;
        
        [MaxLength(50)]
        public string Extension { get; set; } = string.Empty;

        public long FileSize { get; set; }
        public DateTime LastModifiedUtc { get; set; }

        [MaxLength(128)]
        public string? Hash { get; set; }

        public AssetKind Kind { get; set; }

        // 画像固有データ
        public int? Width { get; set; }
        public int? Height { get; set; }
        public double? AspectRatio { get; set; }

        // Code/Script specific data
        [MaxLength(50)]
        public string? Language { get; set; } // e.g. "C#", "Python"
        
        [MaxLength(100)]
        public string? Tool { get; set; } // e.g. "Houdini", "Maya" (from legacy CodeFile)

        /// <summary>
        /// Full-text search index for code content. 
        /// NOT the source of truth for file content (system file is).
        /// </summary>
        public string? ContentIndex { get; set; }
        
        // サムネイル
        [MaxLength(4096)]
        public string? ThumbnailPath { get; set; }
        public DateTime? ThumbnailGeneratedAt { get; set; }

        // ユーザーメタデータ
        public string? UserTagsJson { get; set; }
        public bool IsFavorite { get; set; }
        
        /// <summary>
        /// User rating (0-5). Used for sorting and filtering.
        /// </summary>
        public int Rating { get; set; } = 0;
        
        /// <summary>
        /// Dominant color of the image in HEX format (#FF0000).
        /// Used as placeholder background before image loads.
        /// </summary>
        [MaxLength(10)]
        public string? DominantColor { get; set; }
        
        // 論理削除
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
