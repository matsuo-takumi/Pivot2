using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Pivot.Models;

namespace Pivot.Services
{
	public class DbToJsonMigrationService
	{
		private readonly MetadataService _metadata;
		private readonly string _cacheRoot;

		public DbToJsonMigrationService(MetadataService metadata)
		{
			_metadata = metadata;
			var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			_cacheRoot = Path.Combine(localAppData, "Pivot", "cache");
			Directory.CreateDirectory(_cacheRoot);
		}

		public async Task ExportAssetsAsync(CancellationToken ct = default)
		{
			var list = new List<AssetInfo>();
			await foreach (var a in _metadata.StreamAssetEntriesAsync(500))
			{
				ct.ThrowIfCancellationRequested();
				var info = new AssetInfo
				{
					Path = a.Path,
					Category = a.TagsJson ?? string.Empty,
					Type = a.Type ?? string.Empty,
					LastModified = a.UpdatedAt,
					Size = a.Size,
					Hash = a.Hash,
					Tags = ParseTags(a.TagsJson)
				};
				list.Add(info);
			}
			var assetsPath = Path.Combine(_cacheRoot, "assets.json");
			var tmp = assetsPath + ".tmp";
			await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(list), ct).ConfigureAwait(false);
			var bak = assetsPath + ".bak";
			if (File.Exists(assetsPath)) File.Copy(assetsPath, bak, true);
			File.Move(tmp, assetsPath, true);

			// build tag index
			var tagIndex = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			foreach (var it in list)
			{
				foreach (var t in it.Tags)
				{
					if (!tagIndex.TryGetValue(t, out var arr)) { arr = new List<string>(); tagIndex[t] = arr; }
					arr.Add(it.Path);
				}
			}
			var tagPath = Path.Combine(_cacheRoot, "tag_index.json");
			tmp = tagPath + ".tmp";
			await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(tagIndex), ct).ConfigureAwait(false);
			bak = tagPath + ".bak";
			if (File.Exists(tagPath)) File.Copy(tagPath, bak, true);
			File.Move(tmp, tagPath, true);
		}

		private static List<string> ParseTags(string? tagsJson)
		{
			if (string.IsNullOrWhiteSpace(tagsJson)) return new List<string>();
			try
			{
				var arr = JsonSerializer.Deserialize<List<string>>(tagsJson);
				return arr ?? new List<string>();
			}
			catch
			{
				return new List<string>();
			}
		}
	}
}


