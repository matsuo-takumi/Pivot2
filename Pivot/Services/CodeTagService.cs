using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Pivot.Models;

namespace Pivot.Services
{
    /// <summary>
    /// Service for managing tags on AssetEntities (Code snippets).
    /// </summary>
    public class CodeTagService
    {
        private readonly FilterSettingsService _filterSettings;

        public CodeTagService(FilterSettingsService filterSettings)
        {
            _filterSettings = filterSettings;
        }

        public HashSet<string> GetAllowedPreferenceTags()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var filters = _filterSettings.GetCodeFilters();
            if (filters != null)
            {
                foreach (var filter in filters)
                {
                    if (!string.IsNullOrWhiteSpace(filter.Name)) result.Add(filter.Name.Trim());
                }
            }
            return result;
        }

        public List<string> ParseTagList(string? jsonOrCsv)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(jsonOrCsv)) return result;

            // Try JSON first
            if (jsonOrCsv.TrimStart().StartsWith("["))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<string>>(jsonOrCsv);
                    if (list != null) return list;
                }
                catch { /* Not JSON or invalid */ }
            }

            // Fallback to CSV for legacy compatibility or if distinct storage used
            var parts = jsonOrCsv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in parts)
            {
                var trimmed = item.Trim();
                if (!string.IsNullOrEmpty(trimmed)) result.Add(trimmed);
            }
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private string SerializeTags(List<string> tags)
        {
            return JsonSerializer.Serialize(tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }

        public string NormalizeTagName(string? input)
        {
            return (input ?? string.Empty).Trim();
        }

        public IEnumerable<string> CollectAvailableTags(IEnumerable<AssetEntity> snippets)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var preferenceTags = GetAllowedPreferenceTags();

            foreach (var name in preferenceTags) names.Add(name);

            if (snippets != null)
            {
                foreach (var snippet in snippets)
                {
                    var tags = ParseTagList(snippet.UserTagsJson);
                    foreach (var tag in tags)
                    {
                        if (preferenceTags.Contains(tag)) names.Add(tag);
                    }
                }
            }

            return names.OrderBy(name => name);
        }

        public bool ValidateAndCleanTags(AssetEntity snippet)
        {
            if (string.IsNullOrWhiteSpace(snippet.UserTagsJson)) return false;

            var allowed = GetAllowedPreferenceTags();
            if (allowed.Count == 0) return false;

            var tags = ParseTagList(snippet.UserTagsJson);
            var filtered = tags.Where(tag => allowed.Contains(tag)).ToList();

            if (filtered.Count != tags.Count)
            {
                snippet.UserTagsJson = SerializeTags(filtered);
                return true;
            }
            return false;
        }

        public bool AddTagToSnippet(AssetEntity snippet, string tagName)
        {
            var tags = ParseTagList(snippet.UserTagsJson);
            var normalized = NormalizeTagName(tagName);
            if (string.IsNullOrEmpty(normalized)) return false;

            if (!tags.Any(t => string.Equals(t, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                tags.Add(normalized);
                snippet.UserTagsJson = SerializeTags(tags);
                return true;
            }
            return false;
        }

        public bool RemoveTagFromSnippet(AssetEntity snippet, string tagName)
        {
            var tags = ParseTagList(snippet.UserTagsJson);
            var normalized = NormalizeTagName(tagName);
            if (string.IsNullOrEmpty(normalized)) return false;

            int removed = tags.RemoveAll(t => string.Equals(t, normalized, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                snippet.UserTagsJson = SerializeTags(tags);
                return true;
            }
            return false;
        }
    }
}
