using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Pivot.Models;

namespace Pivot.Services
{
	public class JsonCatalogService : ICatalogService
	{
		private readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);
		private readonly ConcurrentDictionary<string, AssetInfo> _pathToAsset = new(StringComparer.OrdinalIgnoreCase);
		private readonly ConcurrentDictionary<string, HashSet<string>> _tagToPaths = new(StringComparer.OrdinalIgnoreCase);
		private readonly List<string> _roots = new();
		private string _cacheRoot = string.Empty;
		private readonly ConcurrentDictionary<string, (WatcherChangeTypes Change, DateTime At)> _pending = new(StringComparer.OrdinalIgnoreCase);
		private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
		private System.Timers.Timer? _flushTimer;

		private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga", ".tif", ".tiff", ".webp"
		};

		private static readonly HashSet<string> ModelExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".fbx", ".obj", ".usd", ".usdz", ".gltf", ".glb", ".hdr", ".exr"
		};

		private string AssetsPath => Path.Combine(_cacheRoot, "assets.json");
		private string TagIndexPath => Path.Combine(_cacheRoot, "tag_index.json");
		private string MetaOverridesPath => Path.Combine(_cacheRoot, "meta_overrides.json");

		public async Task InitializeAsync(IEnumerable<string> roots, CancellationToken ct = default)
		{
			_roots.Clear();
			_roots.AddRange(roots.Where(Directory.Exists));
			var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			_cacheRoot = Path.Combine(localAppData, "Pivot", "cache");
			Directory.CreateDirectory(_cacheRoot);

			await LoadAsync(ct).ConfigureAwait(false);

			// start flush timer for watcher events
			_flushTimer = new System.Timers.Timer(400) { AutoReset = true, Enabled = true };
			_flushTimer.Elapsed += async (_, __) =>
			{
				try { await FlushPendingAsync(ct).ConfigureAwait(false); } catch { }
			};

			StartWatchers();
			await RescanAsync(ct).ConfigureAwait(false);
		}

		public async IAsyncEnumerable<AssetInfo> StreamAssets(string? category = null, int batch = 500, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
		{
			IEnumerable<AssetInfo> items = _pathToAsset.Values;
			if (!string.IsNullOrWhiteSpace(category)) items = items.Where(a => string.Equals(a.Category, category, StringComparison.OrdinalIgnoreCase));
			var list = items.ToList();
			for (int i = 0; i < list.Count; i += batch)
			{
				ct.ThrowIfCancellationRequested();
				var slice = list.Skip(i).Take(batch);
				foreach (var x in slice) yield return x;
			}
		}

		public Task<IReadOnlyList<AssetInfo>> QueryByTagAsync(string tag, int skip, int take, CancellationToken ct = default)
		{
			if (string.IsNullOrWhiteSpace(tag)) return Task.FromResult<IReadOnlyList<AssetInfo>>(Array.Empty<AssetInfo>());
			if (!_tagToPaths.TryGetValue(tag, out var set)) return Task.FromResult<IReadOnlyList<AssetInfo>>(Array.Empty<AssetInfo>());
			var result = set.Skip(skip).Take(take).Select(p => _pathToAsset.TryGetValue(p, out var a) ? a : null).OfType<AssetInfo>().ToList();
			return Task.FromResult<IReadOnlyList<AssetInfo>>(result);
		}

		public Task<AssetInfo?> GetByPathAsync(string path, CancellationToken ct = default)
		{
			_pathToAsset.TryGetValue(path, out var a);
			return Task.FromResult(a);
		}

		public async Task<AssetMeta?> GetMetaAsync(string path, CancellationToken ct = default)
		{
			var sidecar = path + ".meta.json";
			if (File.Exists(sidecar))
			{
				try
				{
					var json = await File.ReadAllTextAsync(sidecar, ct).ConfigureAwait(false);
					return JsonSerializer.Deserialize<AssetMeta>(json);
				}
				catch { }
			}
			if (File.Exists(MetaOverridesPath))
			{
				try
				{
					var text = await File.ReadAllTextAsync(MetaOverridesPath, ct).ConfigureAwait(false);
					var map = JsonSerializer.Deserialize<Dictionary<string, AssetMeta>>(text) ?? new();
					if (map.TryGetValue(path, out var meta)) return meta;
				}
				catch { }
			}
			return null;
		}

		public async Task UpsertMetaAsync(string path, AssetMeta meta, CancellationToken ct = default)
		{
			var sidecar = path + ".meta.json";
			try
			{
				var dir = Path.GetDirectoryName(sidecar);
				if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
				var json = JsonSerializer.Serialize(meta);
				await File.WriteAllTextAsync(sidecar, json, ct).ConfigureAwait(false);
			}
			catch (UnauthorizedAccessException)
			{
				await _saveLock.WaitAsync(ct).ConfigureAwait(false);
				try
				{
					var map = new Dictionary<string, AssetMeta>(StringComparer.OrdinalIgnoreCase);
					if (File.Exists(MetaOverridesPath))
					{
						var text = await File.ReadAllTextAsync(MetaOverridesPath, ct).ConfigureAwait(false);
						map = JsonSerializer.Deserialize<Dictionary<string, AssetMeta>>(text) ?? map;
					}
					map[path] = meta;
					var tmp = MetaOverridesPath + ".tmp";
					await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(map), ct).ConfigureAwait(false);
					var bak = MetaOverridesPath + ".bak";
					if (File.Exists(MetaOverridesPath)) File.Copy(MetaOverridesPath, bak, true);
					File.Move(tmp, MetaOverridesPath, true);
				}
				finally
				{
					_saveLock.Release();
				}
			}
		}

		public async Task AddTagAsync(string path, string tag, CancellationToken ct = default)
		{
			if (!_pathToAsset.TryGetValue(path, out var a)) return;
			if (!a.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))) a.Tags.Add(tag);
			var set = _tagToPaths.GetOrAdd(tag, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
			set.Add(path);
			await SaveTagIndexAsync(ct).ConfigureAwait(false);
			await SaveAssetsAsync(ct).ConfigureAwait(false);
		}

		public async Task RemoveTagAsync(string path, string tag, CancellationToken ct = default)
		{
			if (_pathToAsset.TryGetValue(path, out var a))
			{
				a.Tags.RemoveAll(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
			}
			if (_tagToPaths.TryGetValue(tag, out var set)) set.Remove(path);
			await SaveTagIndexAsync(ct).ConfigureAwait(false);
			await SaveAssetsAsync(ct).ConfigureAwait(false);
		}

		private async Task LoadAsync(CancellationToken ct)
		{
			// assets
			try
			{
				if (File.Exists(AssetsPath))
				{
					var json = await File.ReadAllTextAsync(AssetsPath, ct).ConfigureAwait(false);
					var list = JsonSerializer.Deserialize<List<AssetInfo>>(json) ?? new();
					_pathToAsset.Clear();
					foreach (var a in list) _pathToAsset[a.Path] = a;
				}
			}
			catch { }
			// tags
			try
			{
				if (File.Exists(TagIndexPath))
				{
					var text = await File.ReadAllTextAsync(TagIndexPath, ct).ConfigureAwait(false);
					var map = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(text) ?? new();
					_tagToPaths.Clear();
					foreach (var kv in map) _tagToPaths[kv.Key] = new HashSet<string>(kv.Value, StringComparer.OrdinalIgnoreCase);
				}
			}
			catch { }
		}

		private async Task SaveAssetsAsync(CancellationToken ct)
		{
			await _saveLock.WaitAsync(ct).ConfigureAwait(false);
			try
			{
				var tmp = AssetsPath + ".tmp";
				var list = _pathToAsset.Values.OrderBy(a => a.Path, StringComparer.OrdinalIgnoreCase).ToList();
				await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(list), ct).ConfigureAwait(false);
				var bak = AssetsPath + ".bak";
				if (File.Exists(AssetsPath)) File.Copy(AssetsPath, bak, true);
				File.Move(tmp, AssetsPath, true);
			}
			finally
			{
				_saveLock.Release();
			}
		}

		public async Task RescanAsync(CancellationToken ct = default)
		{
			var newMap = new ConcurrentDictionary<string, AssetInfo>(StringComparer.OrdinalIgnoreCase);
			foreach (var root in _roots)
			{
				if (!Directory.Exists(root)) continue;
				IEnumerable<string> files;
				try { files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories); }
				catch { continue; }
				foreach (var path in files)
				{
					ct.ThrowIfCancellationRequested();
					try
					{
						var fi = new FileInfo(path);
						var ext = fi.Extension ?? string.Empty;
						if (!IsAssetExtension(ext)) continue;
						var info = BuildAssetInfo(fi);
						if (_pathToAsset.TryGetValue(path, out var existing))
						{
							// preserve tags on rescan
							info.Tags = existing.Tags;
						}
						newMap[path] = info;
					}
					catch { }
				}
			}

			_pathToAsset.Clear();
			foreach (var kv in newMap) _pathToAsset[kv.Key] = kv.Value;
			await SaveAssetsAsync(ct).ConfigureAwait(false);
		}

		private static bool IsAssetExtension(string ext)
		{
			if (string.IsNullOrWhiteSpace(ext)) return false;
			return ImageExtensions.Contains(ext) || ModelExtensions.Contains(ext);
		}

		private static string MapCategory(string ext)
		{
			if (ImageExtensions.Contains(ext)) return "Image";
			if (ModelExtensions.Contains(ext)) return "Model";
			return "Asset";
		}

		private static AssetInfo BuildAssetInfo(FileInfo fi)
		{
			var ext = fi.Extension ?? string.Empty;
			return new AssetInfo
			{
				Path = fi.FullName,
				Category = MapCategory(ext),
				Type = ext,
				LastModified = fi.LastWriteTimeUtc,
				Size = fi.Length,
				Tags = new List<string>()
			};
		}

		private void StartWatchers()
		{
			StopWatchers();
			foreach (var root in _roots)
			{
				try
				{
					var w = new FileSystemWatcher(root)
					{
						NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
						IncludeSubdirectories = true,
						EnableRaisingEvents = true
					};
					w.Created += OnFsEvent;
					w.Changed += OnFsEvent;
					w.Deleted += OnFsEvent;
					w.Renamed += OnRenamed;
					_watchers[root] = w;
				}
				catch { }
			}
		}

		private void StopWatchers()
		{
			foreach (var kv in _watchers)
			{
				try
				{
					kv.Value.Created -= OnFsEvent;
					kv.Value.Changed -= OnFsEvent;
					kv.Value.Deleted -= OnFsEvent;
					kv.Value.Renamed -= OnRenamed;
					kv.Value.Dispose();
				}
				catch { }
			}
			_watchers.Clear();
		}

		private void OnFsEvent(object sender, FileSystemEventArgs e)
		{
			_pending[e.FullPath] = (e.ChangeType, DateTime.UtcNow);
		}

		private void OnRenamed(object sender, RenamedEventArgs e)
		{
			_pending[e.OldFullPath] = (WatcherChangeTypes.Deleted, DateTime.UtcNow);
			_pending[e.FullPath] = (WatcherChangeTypes.Created, DateTime.UtcNow);
		}

		private async Task FlushPendingAsync(CancellationToken ct)
		{
			var now = DateTime.UtcNow;
			var toProcess = new List<(string Path, WatcherChangeTypes Change)>();
			foreach (var kv in _pending.ToArray())
			{
				if ((now - kv.Value.At).TotalMilliseconds >= 350)
				{
					if (_pending.TryRemove(kv.Key, out var v)) toProcess.Add((kv.Key, v.Change));
				}
			}
			if (toProcess.Count == 0) return;

			foreach (var item in toProcess)
			{
				try
				{
					if (item.Change == WatcherChangeTypes.Deleted)
					{
						_pathToAsset.TryRemove(item.Path, out _);
						foreach (var set in _tagToPaths.Values) set.Remove(item.Path);
					}
					else
					{
						if (File.Exists(item.Path))
						{
							var fi = new FileInfo(item.Path);
							if (IsAssetExtension(fi.Extension ?? string.Empty))
							{
								var info = BuildAssetInfo(fi);
								if (_pathToAsset.TryGetValue(item.Path, out var existing)) info.Tags = existing.Tags;
								_pathToAsset[item.Path] = info;
							}
						}
					}
				}
				catch { }
			}
			await SaveAssetsAsync(ct).ConfigureAwait(false);
		}

		public void Dispose()
		{
			try { if (_flushTimer != null) { _flushTimer.Stop(); _flushTimer.Dispose(); _flushTimer = null; } } catch { }
			StopWatchers();
		}

		private async Task SaveTagIndexAsync(CancellationToken ct)
		{
			await _saveLock.WaitAsync(ct).ConfigureAwait(false);
			try
			{
				var tmp = TagIndexPath + ".tmp";
				var map = _tagToPaths.ToDictionary(kv => kv.Key, kv => kv.Value.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase);
				await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(map), ct).ConfigureAwait(false);
				var bak = TagIndexPath + ".bak";
				if (File.Exists(TagIndexPath)) File.Copy(TagIndexPath, bak, true);
				File.Move(tmp, TagIndexPath, true);
			}
			finally
			{
				_saveLock.Release();
			}
		}
	}
}


