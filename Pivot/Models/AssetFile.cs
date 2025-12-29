using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace Pivot.Models
{
    /// <summary>
    /// アセットファイルのデータベースエンティティ。
    /// 画像、3Dモデル、動画などのメタデータを永続化します。
    /// </summary>
    public class AssetFile : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private int _id;
        private string _filePath = string.Empty;
        private string _directory = string.Empty;
        private string _fileName = string.Empty;
        private string _extension = string.Empty;
        private AssetKind _kind = AssetKind.Image;
        private long _fileSize;
        private long _lastModifiedTicks;
        private int _width;
        private int _height;
        private double _aspectRatio = 1.0;
        private string? _thumbnailHash;
        private bool _isIndexed;
        private DateTime? _indexedAt;
        private bool _isDeleted;

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// ファイルのフルパス（ユニークキー）
        /// </summary>
        [Required]
        [MaxLength(1024)]
        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        /// <summary>
        /// 親ディレクトリのパス（検索用インデックス）
        /// </summary>
        [Required]
        [MaxLength(1024)]
        public string Directory
        {
            get => _directory;
            set => SetProperty(ref _directory, value);
        }

        /// <summary>
        /// ファイル名（拡張子含む）
        /// </summary>
        [Required]
        [MaxLength(260)]
        public string FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }

        /// <summary>
        /// 拡張子（ドット付き、例: ".png"）
        /// </summary>
        [MaxLength(16)]
        public string Extension
        {
            get => _extension;
            set => SetProperty(ref _extension, value);
        }

        /// <summary>
        /// アセットの種類
        /// </summary>
        public AssetKind Kind
        {
            get => _kind;
            set => SetProperty(ref _kind, value);
        }

        /// <summary>
        /// ファイルサイズ（バイト）
        /// </summary>
        public long FileSize
        {
            get => _fileSize;
            set => SetProperty(ref _fileSize, value);
        }

        /// <summary>
        /// 最終更新日時（Ticks）- 変更検知用
        /// </summary>
        public long LastModifiedTicks
        {
            get => _lastModifiedTicks;
            set => SetProperty(ref _lastModifiedTicks, value);
        }

        /// <summary>
        /// 画像の幅（ピクセル）
        /// </summary>
        public int Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        /// <summary>
        /// 画像の高さ（ピクセル）
        /// </summary>
        public int Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        /// <summary>
        /// アスペクト比（Width / Height）
        /// </summary>
        public double AspectRatio
        {
            get => _aspectRatio;
            set => SetProperty(ref _aspectRatio, value);
        }

        /// <summary>
        /// サムネイルキャッシュのハッシュキー
        /// </summary>
        [MaxLength(128)]
        public string? ThumbnailHash
        {
            get => _thumbnailHash;
            set => SetProperty(ref _thumbnailHash, value);
        }

        /// <summary>
        /// メタデータがインデックス済みか
        /// </summary>
        public bool IsIndexed
        {
            get => _isIndexed;
            set => SetProperty(ref _isIndexed, value);
        }

        /// <summary>
        /// インデックス完了日時（UTC）
        /// </summary>
        public DateTime? IndexedAt
        {
            get => _indexedAt;
            set => SetProperty(ref _indexedAt, value);
        }

        /// <summary>
        /// 論理削除フラグ（ファイルが見つからない場合）
        /// </summary>
        public bool IsDeleted
        {
            get => _isDeleted;
            set => SetProperty(ref _isDeleted, value);
        }

        // =============== Helper Properties (Not Mapped) ===============

        /// <summary>
        /// 最終更新日時をDateTimeで取得
        /// </summary>
        [NotMapped]
        public DateTime LastModified => new DateTime(_lastModifiedTicks, DateTimeKind.Utc);

        /// <summary>
        /// TemplateItem変換用のサムネイルパス（キャッシュディレクトリから計算）
        /// </summary>
        [NotMapped]
        public string? ThumbnailPath { get; set; }

        // =============== INotifyPropertyChanged ===============

        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
