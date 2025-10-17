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
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // 関連するプロジェクトのIDリスト (多対多のリレーションシップを表現)
        [BsonRef("projects")]
        public List<ProjectEntry> Projects { get; set; } = new List<ProjectEntry>();

        // 関連するFileEntry
        [BsonRef("files")]
        public FileEntry File { get; set; }

        public AssetEntry()
        {
            Path = string.Empty;
            Name = string.Empty;
            Type = string.Empty;
            Hash = string.Empty;
            File = new FileEntry();
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }
    }
}
