using LiteDB; // LiteDB を使用するために追加

namespace Pivot.Models
{
    public class ImageEntry
    {
        [BsonId]
        public int Id { get; set; } // LiteDB のプライマリキー
        [BsonRef("files")] // "files" コレクションへの参照
        public FileEntry File { get; set; } // FileEntry オブジェクト自体を参照
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
