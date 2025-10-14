using SQLite;

namespace Pivot.Models
{
    public class ImageEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int FileId { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string AITagsJson { get; set; } // JSON文字列として保存
        public float Confidence { get; set; }

        public ImageEntry()
        {
            AITagsJson = string.Empty;
        }
    }
}
