using System;

namespace Pivot.Models
{
    /// <summary>
    /// ファイルスキャン結果のキャッシュエントリ。
    /// 前回のスキャン時の状態を記録し、変更検知の高速化に利用します。
    /// </summary>
    public class ScanCacheEntry
    {
        public string FilePath { get; set; } // ファイルの絶対パス（ユニークキー）

        public string Hash { get; set; } // ファイルのハッシュ値

        public long Size { get; set; } // ファイルサイズ（バイト）

        public DateTime LastModifiedUtc { get; set; } // ファイルの最終更新時刻（UTC）

        public DateTime CachedAt { get; set; } // このキャッシュエントリが作成・更新された時刻（UTC）

        public string RootPath { get; set; } // スキャン対象のルートパス（複数ルート時の識別用）

        public ScanCacheEntry()
        {
            FilePath = string.Empty;
            Hash = string.Empty;
            Size = 0;
            LastModifiedUtc = DateTime.UtcNow;
            CachedAt = DateTime.UtcNow;
            RootPath = string.Empty;
        }

        public ScanCacheEntry(string filePath, string hash, long size, DateTime lastModifiedUtc, string rootPath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("FilePath cannot be null or empty.", nameof(filePath));

            FilePath = filePath;
            Hash = hash;
            Size = size;
            LastModifiedUtc = lastModifiedUtc;
            CachedAt = DateTime.UtcNow;
            RootPath = rootPath;
        }
    }
}
