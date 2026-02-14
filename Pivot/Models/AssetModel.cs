using CommunityToolkit.Mvvm.ComponentModel;
using Pivot.Engine.Models;
using System;
using System.IO;

namespace Pivot.Models
{
    public partial class AssetModel : ObservableObject
    {
        public AssetEntity Entity { get; }

        public AssetModel(AssetEntity entity)
        {
            Entity = entity;
        }

        [ObservableProperty]
        private bool _isSelected;

        public int Id => Entity.Id;
        public string FilePath => Entity.FilePath;
        public string FileName => Entity.FileName;
        public string? Extension => Entity.Extension;
        public long FileSize => Entity.FileSize;
        public DateTime LastModifiedUtc => Entity.LastModifiedUtc;
        public AssetKind Kind => Entity.Kind;
        public int? Width => Entity.Width;
        public int? Height => Entity.Height;
        public double? AspectRatio => Entity.AspectRatio;
        public string? ThumbnailPath => Entity.ThumbnailPath;
        public string? UserTagsJson => Entity.UserTagsJson;
        
        public bool IsFavorite 
        { 
            get => Entity.IsFavorite; 
            set 
            {
                if (Entity.IsFavorite != value)
                {
                    Entity.IsFavorite = value;
                    OnPropertyChanged();
                }
            } 
        }

        public int Rating => Entity.Rating;
        
        public int SortOrder
        {
            get => Entity.SortOrder;
            set
            {
                if (Entity.SortOrder != value)
                {
                    Entity.SortOrder = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public string? DominantColor => Entity.DominantColor;
        public string? ContentIndex => Entity.ContentIndex;
        public string? Language => Entity.Language;

        public string DisplayImageSource => !string.IsNullOrEmpty(ThumbnailPath) ? ThumbnailPath : FilePath;

        public string Dimensions => Width.HasValue && Height.HasValue ? $"{Width} x {Height}" : string.Empty;

        public string FileSizeString
        {
            get
            {
                if (FileSize < 1024) return $"{FileSize} B";
                if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F1} KB";
                return $"{FileSize / (1024.0 * 1024.0):F1} MB";
            }
        }
    }
}
