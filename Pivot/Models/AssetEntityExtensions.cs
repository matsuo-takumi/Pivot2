using System.Collections.Generic;
using System.Text.Json;
using Pivot.Engine.Models; // Engineのモデルを拡張

namespace Pivot.Models
{
    public static class AssetEntityExtensions
    {
        // JSON文字列として保存されているタグをリストとして取得する拡張メソッド
        public static IEnumerable<string> GetTags(this AssetEntity asset)
        {
            if (string.IsNullOrEmpty(asset.UserTagsJson)) // Original code used UserTagsJson, user snippet used TagsJson. Pivot2 usually uses UserTagsJson based on previous files.
                return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(asset.UserTagsJson) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
