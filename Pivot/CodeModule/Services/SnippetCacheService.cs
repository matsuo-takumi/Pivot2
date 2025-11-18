using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Pivot.CodeModule.Models;
using Pivot.Services;

namespace Pivot.CodeModule.Services
{
    public sealed class SnippetCacheService
    {
        private const string CacheKey = "Code.SnippetCache";
        private const int CurrentVersion = 1;

        private readonly ISettingsStore _settingsStore;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly object _cacheLock = new object();
        private readonly SemaphoreSlim _persistenceLock = new SemaphoreSlim(1, 1);
        private readonly Task _initializationTask;
        private Dictionary<Guid, CodeFile> _cacheEntries = new Dictionary<Guid, CodeFile>();
        private HashSet<string> _allTags = new(StringComparer.OrdinalIgnoreCase);
        private bool _isDirty = false;

        public SnippetCacheService(ISettingsStore settingsStore)
        {
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            };
            _initializationTask = InitializeAsync();
        }

        public async Task<IReadOnlyList<CodeFile>> GetAllAsync()
        {
            await EnsureInitializedAsync();
            lock (_cacheLock)
            {
                return _cacheEntries.Values.ToList();
            }
        }

        public async Task<CodeFile?> GetAsync(Guid id)
        {
            await EnsureInitializedAsync();
            lock (_cacheLock)
            {
                return _cacheEntries.TryGetValue(id, out var entry) ? entry : null;
            }
        }

        public async Task UpsertAsync(CodeFile snippet)
        {
            if (snippet == null) throw new ArgumentNullException(nameof(snippet));
            await EnsureInitializedAsync();
            lock (_cacheLock)
            {
                _cacheEntries[snippet.Id] = snippet;
            }
            RebuildTagIndex();
            _isDirty = true;
        }

        public async Task RefreshAsync(IEnumerable<CodeFile>? snippets)
        {
            if (snippets == null) return;
            await EnsureInitializedAsync();
            lock (_cacheLock)
            {
                foreach (var snippet in snippets)
                {
                    if (snippet == null) continue;
                    _cacheEntries[snippet.Id] = snippet;
                }
            }
            RebuildTagIndex();
            _isDirty = false;
        }

        public async Task RemoveAsync(Guid id)
        {
            await EnsureInitializedAsync();
            var removed = false;
            lock (_cacheLock)
            {
                removed = _cacheEntries.Remove(id);
            }
            if (removed)
            {
                RebuildTagIndex();
                _isDirty = true;
            }
        }

        private async Task EnsureInitializedAsync()
        {
            await _initializationTask.ConfigureAwait(false);
        }

        private async Task InitializeAsync()
        {
            try
            {
                var json = await _settingsStore.GetAsync(CacheKey).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                {
                    lock (_cacheLock)
                    {
                        _cacheEntries = new Dictionary<Guid, CodeFile>();
                    }
                    return;
                }

                var document = JsonSerializer.Deserialize<SnippetCacheDocument>(json, _serializerOptions);
                if (document == null || document.Items == null)
                {
                    lock (_cacheLock)
                    {
                        _cacheEntries = new Dictionary<Guid, CodeFile>();
                    }
                    return;
                }

                var map = new Dictionary<Guid, CodeFile>();
                foreach (var record in document.Items)
                {
                    if (record == null) continue;
                    map[record.Id] = ToCodeFile(record);
                }

                lock (_cacheLock)
                {
                    _cacheEntries = map;
                }
                RebuildTagIndex();
            }
            catch
            {
                lock (_cacheLock)
                {
                    _cacheEntries = new Dictionary<Guid, CodeFile>();
                    _allTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        private async Task PersistAsync()
        {
            await _persistenceLock.WaitAsync().ConfigureAwait(false);
            try
            {
                List<SnippetCacheRecord> records;
                lock (_cacheLock)
                {
                    records = _cacheEntries.Values.Select(ToRecord).ToList();
                }

                var document = new SnippetCacheDocument
                {
                    Version = CurrentVersion,
                    Items = records
                };

                var payload = JsonSerializer.Serialize(document, _serializerOptions);
                await _settingsStore.UpsertAsync(CacheKey, payload).ConfigureAwait(false);
                _isDirty = false;
            }
            finally
            {
                _persistenceLock.Release();
            }
        }

        public async Task SaveIfDirtyAsync()
        {
            await EnsureInitializedAsync();
            if (!_isDirty) return;
            await PersistAsync().ConfigureAwait(false);
        }

        public async Task<IReadOnlyCollection<string>> GetAllTagsAsync()
        {
            await EnsureInitializedAsync();
            lock (_cacheLock)
            {
                return _allTags.ToList();
            }
        }

        private static SnippetCacheRecord ToRecord(CodeFile snippet)
        {
            return new SnippetCacheRecord
            {
                Id = snippet.Id,
                Title = snippet.Title,
                Language = snippet.Language,
                Tool = snippet.Tool,
                Tags = snippet.Tags,
                Content = snippet.Content,
                Updated = snippet.Updated,
                IsDeleted = snippet.IsDeleted,
                DeletedAt = snippet.DeletedAt,
                AdditionalData = new Dictionary<string, string>()
            };
        }

        private static CodeFile ToCodeFile(SnippetCacheRecord record)
        {
            var snippet = new CodeFile
            {
                Id = record.Id,
                Title = record.Title ?? string.Empty,
                Language = record.Language ?? string.Empty,
                Tool = record.Tool ?? string.Empty,
                Tags = record.Tags ?? string.Empty,
                Content = record.Content ?? string.Empty,
                Updated = record.Updated != default ? record.Updated : DateTime.Now,
                IsDeleted = record.IsDeleted,
                DeletedAt = record.DeletedAt
            };
            return snippet;
        }

        private void RebuildTagIndex()
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            lock (_cacheLock)
            {
                foreach (var snippet in _cacheEntries.Values)
                {
                    foreach (var tag in SplitTags(snippet.Tags))
                    {
                        tags.Add(tag);
                    }
                }
                _allTags = tags;
            }
        }

        private static IEnumerable<string> SplitTags(string? tags)
        {
            if (string.IsNullOrWhiteSpace(tags)) return Array.Empty<string>();
            return tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(t => t.Trim())
                       .Where(t => !string.IsNullOrWhiteSpace(t));
        }

        private sealed class SnippetCacheDocument
        {
            public int Version { get; set; } = CurrentVersion;
            public List<SnippetCacheRecord> Items { get; set; } = new();
        }

        private sealed class SnippetCacheRecord
        {
            public Guid Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Language { get; set; } = string.Empty;
            public string Tool { get; set; } = string.Empty;
            public string Tags { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public DateTime Updated { get; set; }
            public bool IsDeleted { get; set; }
            public DateTime? DeletedAt { get; set; }
            public Dictionary<string, string>? AdditionalData { get; set; }
        }
    }
}

