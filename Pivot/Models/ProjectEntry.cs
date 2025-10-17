using System;
using System.Collections.Generic;
using LiteDB;

namespace Pivot.Models
{
    public class ProjectEntry
    {
        [BsonId]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Path { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // プロジェクトに関連するファイルのIDリスト
        [BsonRef("files")]
        public List<FileEntry> Files { get; set; } = new List<FileEntry>();

        public ProjectEntry()
        {
            Name = string.Empty;
            Description = string.Empty;
            Path = string.Empty;
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }
    }
}
