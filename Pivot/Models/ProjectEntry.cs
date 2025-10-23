using System;
using System.Collections.Generic;

namespace Pivot.Models
{
    public class ProjectEntry
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Path { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // プロジェクトに関連するファイルのIDリスト
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
