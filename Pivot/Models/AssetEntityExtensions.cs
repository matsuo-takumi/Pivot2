using System.Collections.Generic;
using System.Text.Json;
using Pivot.Engine.Models;

namespace Pivot.Models
{
    public static class AssetEntityExtensions
    {
        public static IEnumerable<string> GetTags(this AssetEntity asset)
        {
            if (string.IsNullOrEmpty(asset.UserTagsJson))
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
