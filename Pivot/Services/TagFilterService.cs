using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pivot.Models;

namespace Pivot.Services
{
    public class TagFilterService
    {
        private readonly SettingsService _settingsService;
        private readonly ILogger<TagFilterService> _logger;

        public TagFilterService(SettingsService settingsService, ILogger<TagFilterService> logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        public List<TagDefinition> GetAvailableTags()
        {
            try
            {
                var tags = _settingsService.GetAssetTagDefinitions();
                return tags ?? new List<TagDefinition>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TagFilterService: Failed to get available tags");
                return new List<TagDefinition>();
            }
        }

        public async Task EnsureDefaultsAsync()
        {
            var current = GetAvailableTags();
            if (current == null || current.Count == 0)
            {
                var defaults = new List<TagDefinition>
                {
                    new TagDefinition { Name = "Models", Extensions = new List<string>{ "obj","fbx","stl","glb","gltf","3ds","dae" }, ColorHex = "#FF8A00" },
                    new TagDefinition { Name = "Textures", Extensions = new List<string>{ "jpg","jpeg","png","tga","tiff","bmp","webp" }, ColorHex = "#0078D4" }
                };
                await SaveTagsAsync(defaults);
            }
        }

        public async Task SaveTagsAsync(List<TagDefinition> tags)
        {
            try
            {
                var normalized = tags ?? new List<TagDefinition>();
                await _settingsService.SetAssetTagDefinitionsAsync(normalized);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TagFilterService: Failed to save tags");
            }
        }

        public async Task AddTagAsync(TagDefinition tag)
        {
            var list = GetAvailableTags();
            list.Add(tag);
            await SaveTagsAsync(list);
        }

        public async Task UpdateTagAsync(string originalName, TagDefinition updated)
        {
            var list = GetAvailableTags();
            var idx = list.FindIndex(t => string.Equals(t.Name, originalName, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                list[idx] = updated;
                await SaveTagsAsync(list);
            }
        }

        public async Task DeleteTagAsync(string name)
        {
            var list = GetAvailableTags();
            list.RemoveAll(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            await SaveTagsAsync(list);
        }
    }
}
