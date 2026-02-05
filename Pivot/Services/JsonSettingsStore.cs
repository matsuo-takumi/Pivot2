using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Pivot.Services
{
	public class JsonSettingsStore : ISettingsStore
	{
		private readonly string _settingsPath;
		private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
		private Dictionary<string, string> _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		public JsonSettingsStore()
		{
			var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var root = Path.Combine(localAppData, "Pivot", "cache");
			Directory.CreateDirectory(root);
			_settingsPath = Path.Combine(root, "settings.json");
		}

		public async Task InitializeAsync(CancellationToken ct = default)
		{
			await _lock.WaitAsync(ct).ConfigureAwait(false);
			try
			{
				if (File.Exists(_settingsPath))
				{
					var json = await File.ReadAllTextAsync(_settingsPath, ct).ConfigureAwait(false);
					var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
					_cache = dict ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				}
				else
				{
					_cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				}
			}
			finally
			{
				_lock.Release();
			}
		}

		public async Task<string?> GetAsync(string key, CancellationToken ct = default)
		{
			await _lock.WaitAsync(ct).ConfigureAwait(false);
			try { return _cache.TryGetValue(key, out var v) ? v : null; }
			finally { _lock.Release(); }
		}

		public async Task UpsertAsync(string key, string value, CancellationToken ct = default)
		{
			await _lock.WaitAsync(ct).ConfigureAwait(false);
			try
			{
				_cache[key] = value ?? string.Empty;
				var tmp = _settingsPath + ".tmp";
				var json = JsonSerializer.Serialize(_cache);
				await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);
				var bak = _settingsPath + ".bak";
				if (File.Exists(_settingsPath)) File.Copy(_settingsPath, bak, overwrite: true);
				File.Move(tmp, _settingsPath, overwrite: true);
			}
			finally
			{
				_lock.Release();
			}
		}

		public async Task DeleteAsync(string key, CancellationToken ct = default)
		{
			await _lock.WaitAsync(ct).ConfigureAwait(false);
			try
			{
				if (_cache.Remove(key))
				{
					var tmp = _settingsPath + ".tmp";
					var json = JsonSerializer.Serialize(_cache);
					await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);
					var bak = _settingsPath + ".bak";
					if (File.Exists(_settingsPath)) File.Copy(_settingsPath, bak, overwrite: true);
					File.Move(tmp, _settingsPath, overwrite: true);
				}
			}
			finally
			{
				_lock.Release();
			}
		}

		public async Task<IDictionary<string, string>> GetAllAsync(CancellationToken ct = default)
		{
			await _lock.WaitAsync(ct).ConfigureAwait(false);
			try { return new Dictionary<string, string>(_cache); }
			finally { _lock.Release(); }
		}
	}
}


