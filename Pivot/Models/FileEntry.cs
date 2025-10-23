using System;

namespace Pivot.Models
{
    public class FileEntry
    {
        public int Id { get; set; }
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
