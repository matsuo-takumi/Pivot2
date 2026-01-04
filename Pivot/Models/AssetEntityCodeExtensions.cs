using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Pivot.Models
{
    public static class AssetEntityCodeExtensions
    {
        /// <summary>
        /// Retrieves tags as a list of strings irrespective of storage format (JSON/CSV).
        /// </summary>
        public static List<string> GetTags(this AssetEntity entity)
        {
            if (string.IsNullOrWhiteSpace(entity.UserTagsJson)) return new List<string>();
            try
            {
                // Try JSON
                if (entity.UserTagsJson.TrimStart().StartsWith("["))
                {
                    return JsonSerializer.Deserialize<List<string>>(entity.UserTagsJson) ?? new List<string>();
                }

                // Fallback CSV (Legacy)
                return entity.UserTagsJson.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                          .Select(s => s.Trim())
                                          .Where(s => !string.IsNullOrWhiteSpace(s))
                                          .Distinct(StringComparer.OrdinalIgnoreCase)
                                          .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Updates the tags using a list of strings, serialized as JSON.
        /// </summary>
        public static void SetTags(this AssetEntity entity, IEnumerable<string> tags)
        {
            if (tags == null)
            {
                entity.UserTagsJson = "[]";
                return;
            }
            entity.UserTagsJson = JsonSerializer.Serialize(tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }
    }
}
