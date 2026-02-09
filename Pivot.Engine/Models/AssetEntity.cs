using System;
using System.ComponentModel.DataAnnotations;
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
    public class AssetEntity : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        private bool _isSelected;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

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
        
        /// <summary>
        /// サムネイルがあればそれを、なければ元画像パスを返す（表示用）
        /// </summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string DisplayImageSource => !string.IsNullOrEmpty(ThumbnailPath) ? ThumbnailPath : FilePath;

        // ユーザーメタデータ
        private string? _userTagsJson;
        public string? UserTagsJson 
        { 
            get => _userTagsJson;
            set
            {
                if (_userTagsJson != value)
                {
                    _userTagsJson = value;
                    OnPropertyChanged();
                }
            }
        }
        public bool IsFavorite { get; set; }
        
        /// <summary>
        /// User rating (0-5). Used for sorting and filtering.
        /// </summary>
        /// <summary>
        /// User rating (0-5). Used for sorting and filtering.
        /// </summary>
        public int Rating { get; set; } = 0;

        /// <summary>
        /// Manual sort order.
        /// </summary>
        public int SortOrder { get; set; } = 0;
        
        /// <summary>
        /// Dominant color of the image in HEX format (#FF0000).
        /// Used as placeholder background before image loads.
        /// </summary>
        [MaxLength(10)]
        public string? DominantColor { get; set; }

        // [New in v2.1] Duplicate/Similar search
        public ulong? PerceptualHash { get; set; }

        // [New in v2.1] Advanced color search
        public virtual ICollection<AssetColor> Colors { get; set; } = new List<AssetColor>();
        
        // 論理削除
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
