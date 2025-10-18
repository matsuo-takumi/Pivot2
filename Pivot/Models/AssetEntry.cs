using System;
using System.Collections.Generic;
using LiteDB;

namespace Pivot.Models
{
    public class AssetEntry
    {
        [BsonId]
        public int Id { get; set; }

        public string Path { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public long Size { get; set; }
        public string Hash { get; set; }
        public string TagsJson { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // 関連するプロジェクト（多対多はIDリストやブリッジテーブルで管理することを推奨）
        [BsonRef("projects")]
        public List<ProjectEntry> Projects { get; set; } = new List<ProjectEntry>();

        // 関連するFileEntry（ある場合のみ）
        [BsonRef("files")]
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
