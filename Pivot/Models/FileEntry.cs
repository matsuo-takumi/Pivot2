using SQLite;
using System;

namespace Pivot.Models
{
    public class FileEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public string Path { get; set; }
        public string Type { get; set; }
        public long Size { get; set; }
        public DateTime UpdatedAt { get; set; }
        [Indexed]
        public string Hash { get; set; }

        public FileEntry()
        {
            Path = string.Empty;
            Type = string.Empty;
            Hash = string.Empty;
        }
    }
}
