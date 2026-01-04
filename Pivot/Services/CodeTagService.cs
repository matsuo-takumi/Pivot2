using System;
using System.Collections.Generic;
using System.Linq;
using Pivot.CodeModule.Models;

namespace Pivot.Services
{
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
            try
            {
                var filters = _filterSettings.GetCodeFilters();
                if (filters != null)
                {
                    foreach (var filter in filters)
                    {
                        var name = (filter?.Name ?? string.Empty).Trim();
                        if (!string.IsNullOrEmpty(name))
                        {
                            result.Add(name);
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        public List<string> ParseTagList(string? raw)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return result;
            var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in parts)
            {
                var trimmed = item.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (result.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase))) continue;
                result.Add(trimmed);
            }
            return result;
        }

        public string NormalizeTagName(string? input)
        {
            return (input ?? string.Empty).Trim();
        }

        public string JoinTags(IEnumerable<string> tags)
        {
            return string.Join(", ", tags);
        }

        public IEnumerable<string> CollectAvailableTags(IEnumerable<CodeFile> snippets)
        {
            // Logic from CodeViewModel.CollectTagNames
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var preferenceTags = GetAllowedPreferenceTags();
            
            // 1. Add tags from preferences
            foreach (var name in preferenceTags)
            {
                names.Add(name);
            }
            
            // 2. Add tags from snippets, BUT ONLY if they exist in preferences
            // (Strict filtering based on preferences)
            if (snippets != null)
            {
                foreach (var snippet in snippets)
                {
                    foreach (var tag in ParseTagList(snippet.Tags))
                    {
                        if (preferenceTags.Contains(tag))
                        {
                            names.Add(tag);
                        }
                    }
                }
            }

            return names.OrderBy(name => name);
        }

        /// <summary>
        /// Update snippet tags to only include allowed tags.
        /// Returns true if changes were made.
        /// </summary>
        public bool ValidateAndCleanTags(CodeFile snippet)
        {
            if (string.IsNullOrWhiteSpace(snippet.Tags)) return false;

            var allowed = GetAllowedPreferenceTags();
            if (allowed.Count == 0) return false; // If no prefs, all allowed (or none? existing logic said "If no preferences are set, don't filter")
            // wait, existing logic said:
            // "If no preferences are set, don't filter (allow all tags)" in RemoveInvalidTagsFromSnippets
            // BUT "if (allowed.Count == 0) return" means we do nothing, so we allow all?
            // "GetAllowedPreferenceTagNames" returns list. 
            // In UpdateFilteredTags: "var shouldFilter = allowedTags.Count > 0;"
            // So if count=0, we don't filter.
            
            // So here, if allowed count is 0, we do nothing.
            
            var tags = ParseTagList(snippet.Tags);
            var filtered = tags.Where(tag => allowed.Contains(tag)).ToList();
            
            if (filtered.Count != tags.Count)
            {
                snippet.Tags = filtered.Count > 0 ? JoinTags(filtered) : string.Empty;
                snippet.Updated = DateTime.Now;
                return true;
            }
            return false;
        }

        public bool AddTagToSnippet(CodeFile snippet, string tagName)
        {
             var tags = ParseTagList(snippet.Tags);
             var normalized = NormalizeTagName(tagName);
             if (string.IsNullOrEmpty(normalized)) return false;

             if (!tags.Any(t => string.Equals(t, normalized, StringComparison.OrdinalIgnoreCase)))
             {
                 tags.Add(normalized);
                 snippet.Tags = JoinTags(tags);
                 snippet.Updated = DateTime.Now;
                 return true;
             }
             return false;
        }

        public bool RemoveTagFromSnippet(CodeFile snippet, string tagName)
        {
            var tags = ParseTagList(snippet.Tags);
            var normalized = NormalizeTagName(tagName);
             if (string.IsNullOrEmpty(normalized)) return false;

            int removed = tags.RemoveAll(t => string.Equals(t, normalized, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                snippet.Tags = JoinTags(tags);
                snippet.Updated = DateTime.Now;
                return true;
            }
            return false;
        }

        public IEnumerable<CodeFile> FindSnippetsByTag(IEnumerable<CodeFile> snippets, string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName) || snippets == null) return Enumerable.Empty<CodeFile>();
            var normalized = NormalizeTagName(tagName);
            
            return snippets.Where(s =>
            {
                var tags = ParseTagList(s.Tags);
                return tags.Any(t => string.Equals(t, normalized, StringComparison.OrdinalIgnoreCase));
            });
        }
    }
}
