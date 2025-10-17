using System;
using LiteDB; // LiteDB を使用するために追加

namespace Pivot.Models
{
    public class FileEntry
    {
        [BsonId]
        public int Id { get; set; } // LiteDB のプライマリキー
        public string Path { get; set; }
        public string Type { get; set; }
        public long Size { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Hash { get; set; }

        public FileEntry()
        {
            Path = string.Empty;
            Type = string.Empty;
            Hash = string.Empty;
        }
    }
}
