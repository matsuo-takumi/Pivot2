using System;

namespace Pivot.Models
{
    public class ImageEntry
    {
        public int Id { get; set; }
        public FileEntry File { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string AITagsJson { get; set; } // JSON文字列として保存
        public float Confidence { get; set; }

        public ImageEntry()
        {
            AITagsJson = string.Empty;
            File = new FileEntry(); // null参照を防ぐため初期化
        }
    }
}
