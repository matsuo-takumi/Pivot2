using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Pivot.Engine.Models
{
    [Index(nameof(FilePath), IsUnique = true)]
    [Index(nameof(Directory))]
    [Index(nameof(Kind))]
    [Index(nameof(Hash))]
    [Index(nameof(LastModifiedUtc))]
    [Index(nameof(Kind), nameof(LastModifiedUtc), Name = "IX_Asset_Kind_Date")]
    [Index(nameof(AspectRatio), Name = "IX_Asset_AspectRatio")]
    [Index(nameof(Rating), Name = "IX_Asset_Rating")]
    [Index(nameof(SortOrder), Name = "IX_Asset_SortOrder")]
    [Index(nameof(PerceptualHash), Name = "IX_Asset_PerceptualHash")]
    public record AssetEntity
    {
        [Key]
        public int Id { get; init; }

        [Required]
        [MaxLength(4096)]
        public string FilePath { get; init; } = string.Empty;

        [MaxLength(512)]
        public string FileName { get; init; } = string.Empty;
        
        [MaxLength(4096)]
        public string Directory { get; init; } = string.Empty;
        
        [MaxLength(50)]
        public string Extension { get; init; } = string.Empty;

        public long FileSize { get; init; }
        public DateTime LastModifiedUtc { get; init; }

        [MaxLength(128)]
        public string? Hash { get; init; }

        public AssetKind Kind { get; init; }

        // Image specific data
        public int? Width { get; set; }
        public int? Height { get; set; }
        public double? AspectRatio { get; set; }

        // Code/Script specific data
        [MaxLength(50)]
        public string? Language { get; init; } 
        
        [MaxLength(100)]
        public string? Tool { get; init; } 

        public string? ContentIndex { get; set; }
        
        // Thumbnail
        [MaxLength(4096)]
        public string? ThumbnailPath { get; set; }
        public DateTime? ThumbnailGeneratedAt { get; set; }
        
        // User Metadata
        public string? UserTagsJson { get; set; }
        public bool IsFavorite { get; set; }
        
        public int Rating { get; set; } = 0;

        public int SortOrder { get; set; } = 0;
        
        [MaxLength(10)]
        public string? DominantColor { get; set; }

        public ulong? PerceptualHash { get; set; }

        public virtual ICollection<AssetColor> Colors { get; set; } = new List<AssetColor>();
        
        // Logical Delete
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
