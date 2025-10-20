using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using Pivot.Models; // 蠢・ｦ√↓蠢懊§縺ｦ繝｢繝・Ν繧貞ｮ夂ｾｩ縺吶ｋ
using LiteDB; // LiteDB 繧剃ｽｿ逕ｨ縺吶ｋ縺溘ａ縺ｫ霑ｽ蜉

namespace Pivot.Services
{
    public class MetadataService : IDisposable
    {
        private readonly ILogger<MetadataService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _databasePath;

        public LiteDatabase? Database { get; private set; }
        // 蜈ｨ譖ｸ縺崎ｾｼ縺ｿ謫堺ｽ懊ｒ逶ｴ蛻怜喧縺吶ｋ霆ｽ驥上そ繝槭ヵ繧ｩ
        private readonly SemaphoreSlim _dbWriteLock = new SemaphoreSlim(1, 1);

        public MetadataService(ILogger<MetadataService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _databasePath = Path.Combine(AppContext.BaseDirectory, _configuration["AppSettings:Database:ConnectionString"]?.Replace("Data Source=", "") ?? "pivot.db");

            _logger.LogInformation($"Database path: {_databasePath}");

            // LiteDB 縺ｮ蛻晄悄蛹・(Database 繧ｪ繝悶ず繧ｧ繧ｯ繝医ｒ菴懈・)
            // InitializeDatabase().Wait(); // InitializeDatabase縺ｧ荳蜈・噪縺ｫ蛻晄悄蛹・        }

        public async Task InitializeDatabase()
        {
            if (Database != null) return;

            bool isNewDatabase = false;

            try
            {
                // 1. DB 繝輔ぃ繧､繝ｫ縺悟ｭ伜惠縺吶ｋ縺九メ繧ｧ繝・け・域眠隕・vs 譌｢蟄假ｼ・                isNewDatabase = !File.Exists(_databasePath);
                
                if (isNewDatabase)
                {
                    _logger.LogInformation("Creating new database at: {DatabasePath}", _databasePath);
                }
                else
                {
                    _logger.LogInformation("Opening existing database at: {DatabasePath}", _databasePath);
                }

                // 2. Database 繧偵が繝ｼ繝励Φ
                Database = new LiteDatabase(_databasePath);

                // 3. 繧ｳ繝ｬ繧ｯ繧ｷ繝ｧ繝ｳ蛻晄悄蛹・                InitializeCollections();

                _logger.LogInformation("Database initialized and tables created.");

                // 4. 繝・・繧ｿ繝吶・繧ｹ菫ｮ蠕ｩ蜃ｦ逅・ｼ域里蟄・DB 縺ｮ蝣ｴ蜷医・縺ｿ・・                if (!isNewDatabase)
                {
                    var preferences = Database.GetCollection<PreferenceEntry>();
                    await Task.Run(() => RepairDatabase(preferences));
                }
            }
            catch (LiteException ex)
            {
                _logger.LogError($"LiteDB error: {ex.Message}. Attempting to recover by deleting and recreating database...");
                
                // DB繝輔ぃ繧､繝ｫ縺檎ｴ謳阪＠縺ｦ縺・ｋ蝣ｴ蜷医√ヰ繝・け繧｢繝・・縺励※譁ｰ隕丈ｽ懈・
                if (File.Exists(_databasePath))
                {
                    try
                    {
                        // 譌｢蟄倥・ Database 繧ｪ繝悶ず繧ｧ繧ｯ繝医ｒ遐ｴ譽・                        Database?.Dispose();
                        Database = null;

                        string backupPath = _databasePath + ".backup." + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        File.Move(_databasePath, backupPath, true);
                        _logger.LogInformation($"Corrupted database backed up to: {backupPath}");
                    }
                    catch (Exception backupEx)
                    {
                        _logger.LogError($"Failed to backup corrupted database: {backupEx.Message}");
                    }
                }

                // 繝・・繧ｿ繝吶・繧ｹ繧呈眠隕丈ｽ懈・
                try
                {
                    _logger.LogInformation("Creating new database at: {DatabasePath}", _databasePath);
                    Database = new LiteDatabase(_databasePath);
                    InitializeCollections();
                    _logger.LogInformation("Database successfully recovered with new file.");
                }
                catch (Exception recoverEx)
                {
                    _logger.LogError(recoverEx, "Failed to recover database. The application may not function correctly.");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize database.");
                throw;
            }
        }

        private void InitializeCollections()
        {
            if (Database == null)
                throw new InvalidOperationException("Database is null");

            // 3. 繧ｳ繝ｬ繧ｯ繧ｷ繝ｧ繝ｳ蛻晄悄蛹・            var files = Database.GetCollection<FileEntry>();
            files.EnsureIndex(f => f.Path, true); // Path 縺ｯ繝ｦ繝九・繧ｯ繧､繝ｳ繝・ャ繧ｯ繧ｹ
            files.EnsureIndex(f => f.Hash, false); // Hash 縺ｯ髱槭Θ繝九・繧ｯ繧､繝ｳ繝・ャ繧ｯ繧ｹ

            var images = Database.GetCollection<ImageEntry>();
            images.EnsureIndex(i => i.File.Id); // FileEntry 縺ｸ縺ｮ蜿ら・繧､繝ｳ繝・ャ繧ｯ繧ｹ

            // ProjectEntry 縺ｮ繧ｳ繝ｬ繧ｯ繧ｷ繝ｧ繝ｳ繧ょ・譛溷喧
            var projects = Database.GetCollection<ProjectEntry>();
            projects.EnsureIndex(p => p.Name, true); // Name 縺ｯ繝ｦ繝九・繧ｯ繧､繝ｳ繝・ャ繧ｯ繧ｹ

            // 逕ｻ蜒城未騾｣縺ｮ繧ｳ繝ｬ繧ｯ繧ｷ繝ｧ繝ｳ繧ょ・譛溷喧
            images.EnsureIndex(i => i.Width);
            images.EnsureIndex(i => i.Height);
            // Assets 繧ｳ繝ｬ繧ｯ繧ｷ繝ｧ繝ｳ蛻晄悄蛹・            var assets = Database.GetCollection<AssetEntry>();
            // Use string field names to avoid ambiguous member resolution in expression trees
            assets.EnsureIndex("Path", true);
            assets.EnsureIndex("Hash", false);

            // Scripts 繧ｳ繝ｬ繧ｯ繧ｷ繝ｧ繝ｳ蛻晄悄蛹・            var scripts = Database.GetCollection<ScriptEntry>();
            scripts.EnsureIndex("Path", true);  // Path 縺ｯ繝ｦ繝九・繧ｯ繧､繝ｳ繝・ャ繧ｯ繧ｹ
            scripts.EnsureIndex("Language", false);  // Language 縺ｯ髱槭Θ繝九・繧ｯ繧､繝ｳ繝・ャ繧ｯ繧ｹ
            scripts.EnsureIndex("TargetApplication", false);  // TargetApplication 縺ｯ髱槭Θ繝九・繧ｯ繧､繝ｳ繝・ャ繧ｯ繧ｹ
            scripts.EnsureIndex("Category", false);  // Category 縺ｯ髱槭Θ繝九・繧ｯ繧､繝ｳ繝・ャ繧ｯ繧ｹ

            // UnrealPresets 繧ｳ繝ｬ繧ｯ繧ｷ繝ｧ繝ｳ蛻晄悄蛹・            var presets = Database.GetCollection<UnrealPresetEntry>();
            presets.EnsureIndex("Name", false);  // Name 縺ｯ髱槭Θ繝九・繧ｯ繧､繝ｳ繝・ャ繧ｯ繧ｹ
            presets.EnsureIndex("UprojectPath", false);  // UprojectPath 縺ｯ髱槭Θ繝九・繧ｯ繧､繝ｳ繝・ャ繧ｯ繧ｹ
            presets.EnsureIndex("Category", false);  // Category 縺ｯ髱槭Θ繝九・繧ｯ繧､繝ｳ繝・ャ繧ｯ繧ｹ
            presets.EnsureIndex("ProjectName", false);  // ProjectName 縺ｯ髱槭Θ繝九・繧ｯ繧､繝ｳ繝・ャ繧ｯ繧ｹ

            // Preferences 繧ｳ繝ｬ繧ｯ繧ｷ繝ｧ繝ｳ蛻晄悄蛹・            var preferences = Database.GetCollection<PreferenceEntry>();
            // Do NOT create unique index on 'Key' because 'Key' is BsonId (stored as _id)
            // If an old incorrect index exists, drop it to avoid duplicate-null errors
            try { preferences.DropIndex("Key"); } catch { /* ignore if not exists */ }

            // ScanCache 繧ｳ繝ｬ繧ｯ繧ｷ繝ｧ繝ｳ蛻晄悄蛹・            var scanCache = Database.GetCollection<ScanCacheEntry>();
            scanCache.EnsureIndex("RootPath", false);  // RootPath 縺ｯ髱槭Θ繝九・繧ｯ繧､繝ｳ繝・ャ繧ｯ繧ｹ・医せ繧ｭ繝｣繝ｳ遽・峇縺ｧ縺ｮ讀懃ｴ｢逕ｨ・・            scanCache.EnsureIndex("CachedAt", false);  // CachedAt 縺ｯ髱槭Θ繝九・繧ｯ繧､繝ｳ繝・ャ繧ｯ繧ｹ・亥商縺・く繝｣繝・す繝･蜑企勁逕ｨ・・        }

        private async Task ExecuteWithWriteLockAsync(Func<Task> action)
        {
            await _dbWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await action().ConfigureAwait(false);
            }
            finally
            {
                _dbWriteLock.Release();
            }
        }

        private async Task<T> ExecuteWithWriteLockAsync<T>(Func<Task<T>> action)
        {
            await _dbWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await action().ConfigureAwait(false);
            }
            finally
            {
                _dbWriteLock.Release();
            }
        }

        /// <summary>
        /// 荳肴ｭ｣縺ｪ繧ｨ繝ｳ繝医Μ繧貞炎髯､縺励※繝・・繧ｿ繝吶・繧ｹ繧剃ｿｮ蠕ｩ縺励∪縺吶・        /// </summary>
        private void RepairDatabase(ILiteCollection<PreferenceEntry> preferences)
        {
            try
            {
                if (Database == null)
                {
                    _logger.LogWarning("RepairDatabase called but Database is null. Skipping repair.");
                    return;
                }
                _logger.LogInformation("Starting database repair check...");

                // Access raw BSON collection by name to inspect _id values directly
                var raw = Database.GetCollection<BsonDocument>(preferences.Name);

                var allDocs = raw.FindAll().ToList();
                _logger.LogDebug("RepairDatabase: total docs in collection '{Name}': {Count}", preferences.Name, allDocs.Count);

                // Partition valid and invalid docs
                var validDocs = new List<BsonDocument>();
                var invalidDocs = new List<BsonDocument>();

                foreach (var doc in allDocs)
                {
                    if (!doc.ContainsKey("_id") || doc["_id"].IsNull)
                    {
                        invalidDocs.Add(doc);
                        continue;
                    }

                    var id = doc["_id"];

                    // We expect keys to be strings (preference keys)
                    if (id.IsString)
                    {
                        var s = id.AsString;
                        if (string.IsNullOrWhiteSpace(s))
                        {
                            invalidDocs.Add(doc);
                        }
                        else
                        {
                            validDocs.Add(doc);
                        }
                    }
                    else
                    {
                        // non-string _id is considered invalid for PreferenceEntry
                        invalidDocs.Add(doc);
                    }
                }

                if (invalidDocs.Count > 0)
                {
                    _logger.LogWarning("Found {InvalidEntryCount} invalid preference documents (null/missing/_id not string). Rebuilding collection without them...", invalidDocs.Count);

                    // Rebuild collection: delete all and insert only valid docs
                    raw.DeleteAll();

                    // Ensure we reinsert valid docs (preserve other fields)
                    foreach (var v in validDocs)
                    {
                        try
                        {
                            raw.Insert(v);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to reinsert preference document with _id: {Id}", v.ContainsKey("_id") ? v["_id"].ToString() : "(missing)");
                        }
                    }

                    _logger.LogInformation("Database repair completed. Removed {InvalidEntryCount} invalid documents and reinserted {ValidCount} valid documents.", invalidDocs.Count, validDocs.Count);
                }
                else
                {
                    _logger.LogInformation("Database repair check completed. No invalid entries found.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database repair failed. This may indicate a corrupted database. Consider backing up and deleting the database file at: {DatabasePath}", _databasePath);
                // 菫ｮ蠕ｩ螟ｱ謨励＠縺ｦ繧ゅい繝励Μ繧ｱ繝ｼ繧ｷ繝ｧ繝ｳ繧堤ｶ咏ｶ壹☆繧具ｼ井ｾ句､悶・謚輔￡縺ｪ縺・ｼ・            }
        }

        public async Task UpsertFileAsync(string path, string type, long size, DateTime updatedAt, string hash)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
            var files = Database.GetCollection<FileEntry>();
            var newEntry = new FileEntry
            {
                Path = path,
                Type = type,
                Size = size,
                UpdatedAt = updatedAt,
                Hash = hash
            };
            
                await Task.Run(() =>
                {
            var existingEntry = files.FindOne(f => f.Path == path);
            if (existingEntry != null)
            {
                        newEntry.Id = existingEntry.Id;
                        files.Update(newEntry);
            }
            else
            {
                        files.Insert(newEntry);
            }
                }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        public async Task DeleteFileAsync(string path)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
            var files = Database.GetCollection<FileEntry>();
                await Task.Run(() =>
                {
            var matches = files.Find(Query.EQ("Path", path)).ToList();
            foreach (var m in matches)
            {
                files.Delete(m.Id);
            }
                }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        public async Task<List<FileEntry>> GetFilesAsync(int skip, int take)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var files = Database.GetCollection<FileEntry>();
            var sw = Stopwatch.StartNew();
            var result = await Task.Run(() => files.Find(Query.All("UpdatedAt", Query.Descending), skip, take).ToList());
            sw.Stop();
            _logger.LogDebug("GetFilesAsync skip={Skip} take={Take} fetched={Count} in {Ms} ms", skip, take, result.Count, sw.ElapsedMilliseconds);
            return result;
        }

        // 繧ｹ繝医Μ繝ｼ繝溘Φ繧ｰ蜿門ｾ暦ｼ亥､ｧ隕乗ｨ｡荳隕ｧ蜷代￠・峨・        public async IAsyncEnumerable<FileEntry> StreamFilesAsync(int batchSize = 500)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var files = Database.GetCollection<FileEntry>();
            int skip = 0;
            while (true)
            {
                var batch = await Task.Run(() => files.Find(Query.All("UpdatedAt", Query.Descending), skip, batchSize).ToList());
                if (batch.Count == 0) yield break;
                foreach (var item in batch) yield return item;
                skip += batch.Count;
            }
        }

        // FileEntry 繧・ID 縺ｧ蜿門ｾ・        public async Task<FileEntry?> GetFileEntryByIdAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var files = Database.GetCollection<FileEntry>();
            return await Task.Run(() => files.FindById(id));
        }

        // FileEntry 繧・Path 縺ｧ蜿門ｾ・        public async Task<FileEntry?> GetFileEntryByPathAsync(string path)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var files = Database.GetCollection<FileEntry>();
            return await Task.Run(() => files.FindOne(f => f.Path == path));
        }

        // FileEntry 繧呈峩譁ｰ
        public async Task UpdateFileEntryAsync(FileEntry fileEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var files = Database.GetCollection<FileEntry>();
            await Task.Run(() => files.Update(fileEntry));
        }

        // FileEntry 繧貞炎髯､
        public async Task DeleteFileEntryAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var files = Database.GetCollection<FileEntry>();
            await Task.Run(() => files.Delete(id));
        }

        // ImageEntry 縺ｮ霑ｽ蜉
        public async Task AddImageEntryAsync(ImageEntry imageEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
            var images = Database.GetCollection<ImageEntry>();
                await Task.Run(() => images.Insert(imageEntry)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // ImageEntry 繧・ID 縺ｧ蜿門ｾ・        public async Task<ImageEntry?> GetImageEntryByIdAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var images = Database.GetCollection<ImageEntry>();
            return await Task.Run(() => images.Include(i => i.File).FindById(id));
        }

        // FileEntry 縺ｮ ID 縺ｫ邏舌▼縺・ImageEntry 繧貞叙蠕・        public async Task<ImageEntry?> GetImageEntryByFileIdAsync(int fileId)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var images = Database.GetCollection<ImageEntry>();
            return await Task.Run(() => images.Include(i => i.File).FindOne(i => i.File.Id == fileId));
        }

        // ImageEntry 繧呈峩譁ｰ
        public async Task UpdateImageEntryAsync(ImageEntry imageEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
            var images = Database.GetCollection<ImageEntry>();
                await Task.Run(() => images.Update(imageEntry)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // ImageEntry 繧貞炎髯､
        public async Task DeleteImageEntryAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
            var images = Database.GetCollection<ImageEntry>();
                await Task.Run(() => images.Delete(id)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // 蜈ｨ莉ｶ蜿門ｾ励・螟ｧ隕乗ｨ｡繝・・繧ｿ縺ｧ髱樊耳螂ｨ縲ゅ・繝ｼ繧ｸ繝ｳ繧ｰAPI繧貞茜逕ｨ縺励※縺上□縺輔＞縲・        [Obsolete("Use GetImageEntriesAsync(skip,take) instead.")]
        public async Task<List<ImageEntry>> GetAllImageEntriesAsync()
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            return await GetImageEntriesAsync(0, 200); // 螳牙・縺ｪ繝・ヵ繧ｩ繝ｫ繝・        }

        // ImageEntry 繧偵・繝ｼ繧ｸ繝ｳ繧ｰ縺ｧ蜿門ｾ・        public async Task<List<ImageEntry>> GetImageEntriesAsync(int skip, int take)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var images = Database.GetCollection<ImageEntry>();
            var sw = Stopwatch.StartNew();
            var result = await Task.Run(() => images.Include(i => i.File).Find(Query.All(), skip, take).ToList());
            sw.Stop();
            _logger.LogDebug("GetImageEntriesAsync skip={Skip} take={Take} fetched={Count} in {Ms} ms", skip, take, result.Count, sw.ElapsedMilliseconds);
            return result;
        }

        public async IAsyncEnumerable<ImageEntry> StreamImageEntriesAsync(int batchSize = 500)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var images = Database.GetCollection<ImageEntry>();
            int skip = 0;
            while (true)
            {
                var batch = await Task.Run(() => images.Include(i => i.File).Find(Query.All(), skip, batchSize).ToList());
                if (batch.Count == 0) yield break;
                foreach (var item in batch) yield return item;
                skip += batch.Count;
            }
        }

        // ProjectEntry 縺ｮ霑ｽ蜉
        public async Task AddProjectEntryAsync(ProjectEntry projectEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
            var projects = Database.GetCollection<ProjectEntry>();
                await Task.Run(() => projects.Insert(projectEntry)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // ProjectEntry 繧・ID 縺ｧ蜿門ｾ・        public async Task<ProjectEntry?> GetProjectEntryByIdAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var projects = Database.GetCollection<ProjectEntry>();
            return await Task.Run(() => projects.Include(p => p.Files).FindById(id));
        }

        // ProjectEntry 繧・Name 縺ｧ蜿門ｾ・        public async Task<ProjectEntry?> GetProjectEntryByNameAsync(string name)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var projects = Database.GetCollection<ProjectEntry>();
            return await Task.Run(() => projects.Include(p => p.Files).FindOne(p => p.Name == name));
        }

        // ProjectEntry 繧呈峩譁ｰ
        public async Task UpdateProjectEntryAsync(ProjectEntry projectEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
            var projects = Database.GetCollection<ProjectEntry>();
                await Task.Run(() => projects.Update(projectEntry)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // ProjectEntry 繧貞炎髯､
        public async Task DeleteProjectEntryAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
            var projects = Database.GetCollection<ProjectEntry>();
                await Task.Run(() => projects.Delete(id)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // 蜈ｨ莉ｶ蜿門ｾ励・螟ｧ隕乗ｨ｡繝・・繧ｿ縺ｧ髱樊耳螂ｨ縲ゅ・繝ｼ繧ｸ繝ｳ繧ｰAPI繧貞茜逕ｨ縺励※縺上□縺輔＞縲・        [Obsolete("Use GetProjectEntriesAsync(skip,take) instead.")]
        public async Task<List<ProjectEntry>> GetAllProjectEntriesAsync()
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            return await GetProjectEntriesAsync(0, 200);
        }

        // ProjectEntry 繧偵・繝ｼ繧ｸ繝ｳ繧ｰ縺ｧ蜿門ｾ・        public async Task<List<ProjectEntry>> GetProjectEntriesAsync(int skip, int take)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var projects = Database.GetCollection<ProjectEntry>();
            var sw = Stopwatch.StartNew();
            var result = await Task.Run(() => projects
                .Include(p => p.Files)
                .Find(Query.All("UpdatedAt", Query.Descending), skip, take)
                .ToList());
            sw.Stop();
            _logger.LogDebug("GetProjectEntriesAsync skip={Skip} take={Take} fetched={Count} in {Ms} ms", skip, take, result.Count, sw.ElapsedMilliseconds);
            return result;
        }

        public async IAsyncEnumerable<ProjectEntry> StreamProjectEntriesAsync(int batchSize = 200)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var projects = Database.GetCollection<ProjectEntry>();
            int skip = 0;
            while (true)
            {
                var batch = await Task.Run(() => projects.Include(p => p.Files).Find(Query.All("UpdatedAt", Query.Descending), skip, batchSize).ToList());
                if (batch.Count == 0) yield break;
                foreach (var item in batch) yield return item;
                skip += batch.Count;
            }
        }

        // Name 繧偵く繝ｼ縺ｨ縺励※ Upsert・亥ｭ伜惠縺吶ｌ縺ｰ譖ｴ譁ｰ縲√↑縺代ｌ縺ｰ菴懈・・・        public async Task<ProjectEntry> UpsertProjectByNameAsync(string name, string? description = null, string? path = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var projects = Database.GetCollection<ProjectEntry>();

            return await Task.Run(() =>
            {
                var existing = projects.FindOne(p => p.Name == name);
                if (existing != null)
                {
                    if (description != null) existing.Description = description;
                    if (path != null) existing.Path = path;
                    existing.UpdatedAt = DateTime.Now;
                    projects.Update(existing);
                    return existing;
                }

                var newProject = new ProjectEntry
                {
                    Name = name,
                    Description = description ?? string.Empty,
                    Path = path ?? string.Empty,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                projects.Insert(newProject);
                return newProject;
            });
        }

        // AssetEntry 縺ｮ霑ｽ蜉
        public async Task AddAssetEntryAsync(AssetEntry assetEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
            var assets = Database.GetCollection<AssetEntry>();
                await Task.Run(() => assets.Insert(assetEntry)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // AssetEntry 繧・ID 縺ｧ蜿門ｾ・        public async Task<AssetEntry?> GetAssetEntryByIdAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var assets = Database.GetCollection<AssetEntry>();
            return await Task.Run(() => assets.Include(a => a.File).FindById(id));
        }

        // Path 縺ｫ繧医ｋ蜿門ｾ・        public async Task<AssetEntry?> GetAssetEntryByPathAsync(string path)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var assets = Database.GetCollection<AssetEntry>();
            return await Task.Run(() => assets.Include(a => a.File).FindOne(a => a.Path == path));
        }

        // AssetEntry 繧偵・繝ｼ繧ｸ繝ｳ繧ｰ縺ｧ蜿門ｾ・        public async Task<List<AssetEntry>> GetAssetEntriesAsync(int skip, int take)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var assets = Database.GetCollection<AssetEntry>();
            var sw = Stopwatch.StartNew();
            var result = await Task.Run(() => assets.Include(a => a.File).Find(Query.All("UpdatedAt", Query.Descending), skip, take).ToList());
            sw.Stop();
            _logger.LogDebug("GetAssetEntriesAsync skip={Skip} take={Take} fetched={Count} in {Ms} ms", skip, take, result.Count, sw.ElapsedMilliseconds);
            return result;
        }

        public async IAsyncEnumerable<AssetEntry> StreamAssetEntriesAsync(int batchSize = 500)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var assets = Database.GetCollection<AssetEntry>();
            int skip = 0;
            while (true)
            {
                var batch = await Task.Run(() => assets.Include(a => a.File).Find(Query.All("UpdatedAt", Query.Descending), skip, batchSize).ToList());
                if (batch.Count == 0) yield break;
                foreach (var item in batch) yield return item;
                skip += batch.Count;
            }
        }

        // AssetEntry 繧呈峩譁ｰ
        public async Task UpdateAssetEntryAsync(AssetEntry assetEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
            var assets = Database.GetCollection<AssetEntry>();
                await Task.Run(() => assets.Update(assetEntry)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // AssetEntry 繧貞炎髯､
        public async Task DeleteAssetEntryAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
            var assets = Database.GetCollection<AssetEntry>();
                await Task.Run(() => assets.Delete(id)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // ScriptEntry 縺ｮ霑ｽ蜉
        public async Task AddScriptEntryAsync(ScriptEntry scriptEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
                var scripts = Database.GetCollection<ScriptEntry>();
                await Task.Run(() => scripts.Insert(scriptEntry)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // ScriptEntry 繧・ID 縺ｧ蜿門ｾ・        public async Task<ScriptEntry?> GetScriptEntryByIdAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            return await Task.Run(() => scripts.FindById(id));
        }

        // ScriptEntry 繧・Path 縺ｧ蜿門ｾ・        public async Task<ScriptEntry?> GetScriptEntryByPathAsync(string path)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            return await Task.Run(() => scripts.FindOne(s => s.Path == path));
        }

        // ScriptEntry 繧・Name 縺ｧ蜿門ｾ・        public async Task<ScriptEntry?> GetScriptEntryByNameAsync(string name)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            return await Task.Run(() => scripts.FindOne(s => s.Name == name));
        }

        // 迚ｹ螳壹・險隱槭・繧ｹ繧ｯ繝ｪ繝励ヨ繧偵☆縺ｹ縺ｦ蜿門ｾ・        public async Task<List<ScriptEntry>> GetScriptsByLanguageAsync(string language)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            return await Task.Run(() => scripts.Find(s => s.Language == language).ToList());
        }

        // 迚ｹ螳壹・繧｢繝励Μ繧ｱ繝ｼ繧ｷ繝ｧ繝ｳ蟇ｾ蠢懊・繧ｹ繧ｯ繝ｪ繝励ヨ繧偵☆縺ｹ縺ｦ蜿門ｾ・        public async Task<List<ScriptEntry>> GetScriptsByApplicationAsync(string targetApplication)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            return await Task.Run(() => scripts.Find(s => s.TargetApplication == targetApplication).ToList());
        }

        // 迚ｹ螳壹・繧ｫ繝・ざ繝ｪ縺ｮ繧ｹ繧ｯ繝ｪ繝励ヨ繧偵☆縺ｹ縺ｦ蜿門ｾ・        public async Task<List<ScriptEntry>> GetScriptsByCategoryAsync(string category)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            return await Task.Run(() => scripts.Find(s => s.Category == category).ToList());
        }

        // 譛牙柑縺ｪ繧ｹ繧ｯ繝ｪ繝励ヨ繧偵☆縺ｹ縺ｦ蜿門ｾ・        public async Task<List<ScriptEntry>> GetActiveScriptsAsync()
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            return await Task.Run(() => scripts.Find(s => s.IsActive == true).ToList());
        }

        // ScriptEntry 繧偵・繝ｼ繧ｸ繝ｳ繧ｰ縺ｧ蜿門ｾ・        public async Task<List<ScriptEntry>> GetScriptEntriesAsync(int skip, int take)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            var sw = Stopwatch.StartNew();
            var result = await Task.Run(() => scripts.Find(Query.All("UpdatedAt", Query.Descending), skip, take).ToList());
            sw.Stop();
            _logger.LogDebug("GetScriptEntriesAsync skip={Skip} take={Take} fetched={Count} in {Ms} ms", skip, take, result.Count, sw.ElapsedMilliseconds);
            return result;
        }

        public async IAsyncEnumerable<ScriptEntry> StreamScriptEntriesAsync(int batchSize = 500)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            int skip = 0;
            while (true)
            {
                var batch = await Task.Run(() => scripts.Find(Query.All("UpdatedAt", Query.Descending), skip, batchSize).ToList());
                if (batch.Count == 0) yield break;
                foreach (var item in batch) yield return item;
                skip += batch.Count;
            }
        }

        // ScriptEntry 繧呈峩譁ｰ
        public async Task UpdateScriptEntryAsync(ScriptEntry scriptEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
                var scripts = Database.GetCollection<ScriptEntry>();
                await Task.Run(() => scripts.Update(scriptEntry)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // ScriptEntry 繧貞炎髯､
        public async Task DeleteScriptEntryAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
                var scripts = Database.GetCollection<ScriptEntry>();
                await Task.Run(() => scripts.Delete(id)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // Path 縺ｧ蜑企勁
        public async Task DeleteScriptByPathAsync(string path)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
                var scripts = Database.GetCollection<ScriptEntry>();
                await Task.Run(() =>
                {
                    var s = scripts.FindOne(x => x.Path == path);
                    if (s != null)
                    {
                        scripts.Delete(s.Id);
                    }
                }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // Upsert: Path 繧偵く繝ｼ縺ｫ縲∝ｭ伜惠縺吶ｌ縺ｰ譖ｴ譁ｰ縲√↑縺代ｌ縺ｰ菴懈・
        public async Task<ScriptEntry> UpsertScriptByPathAsync(
            string path,
            string name,
            string language,
            string targetApplication,
            string? codeContent = null,
            string? description = null,
            string? category = null,
            string? version = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            return await ExecuteWithWriteLockAsync(async () =>
            {
                var scripts = Database.GetCollection<ScriptEntry>();
                return await Task.Run(() =>
                {
                    var existing = scripts.FindOne(s => s.Path == path);
                    if (existing != null)
                    {
                        if (!string.IsNullOrEmpty(name)) existing.Name = name;
                        if (!string.IsNullOrEmpty(language)) existing.Language = language;
                        if (!string.IsNullOrEmpty(targetApplication)) existing.TargetApplication = targetApplication;
                        if (codeContent != null) existing.CodeContent = codeContent;
                        if (description != null) existing.Description = description;
                        if (category != null) existing.Category = category;
                        if (version != null) existing.Version = version;
                        existing.UpdatedAt = DateTime.UtcNow;
                        scripts.Update(existing);
                        return existing;
                    }

                    var newScript = new ScriptEntry
                    {
                        Path = path,
                        Name = name,
                        Language = language,
                        TargetApplication = targetApplication,
                        CodeContent = codeContent ?? string.Empty,
                        Description = description ?? string.Empty,
                        Category = category ?? string.Empty,
                        Version = version ?? "1.0.0",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    scripts.Insert(newScript);
                    return newScript;
                }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // UnrealPresetEntry 縺ｮ霑ｽ蜉
        public async Task AddUnrealPresetEntryAsync(UnrealPresetEntry presetEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
                var presets = Database.GetCollection<UnrealPresetEntry>();
                await Task.Run(() => presets.Insert(presetEntry)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // UnrealPresetEntry 繧・ID 縺ｧ蜿門ｾ・        public async Task<UnrealPresetEntry?> GetUnrealPresetEntryByIdAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            return await Task.Run(() => presets.FindById(id));
        }

        // UnrealPresetEntry 繧・Name 縺ｧ蜿門ｾ・        public async Task<UnrealPresetEntry?> GetUnrealPresetEntryByNameAsync(string name)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            return await Task.Run(() => presets.FindOne(p => p.Name == name));
        }

        // 迚ｹ螳壹・ .uproject 縺ｫ髢｢騾｣縺吶ｋ繝励Μ繧ｻ繝・ヨ繧偵☆縺ｹ縺ｦ蜿門ｾ・        public async Task<List<UnrealPresetEntry>> GetPresetsByUprojectAsync(string uprojectPath)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            return await Task.Run(() => presets.Find(p => p.UprojectPath == uprojectPath).ToList());
        }

        // 迚ｹ螳壹・繧ｫ繝・ざ繝ｪ縺ｮ繝励Μ繧ｻ繝・ヨ繧偵☆縺ｹ縺ｦ蜿門ｾ・        public async Task<List<UnrealPresetEntry>> GetPresetsByCategoryAsync(string category)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            return await Task.Run(() => presets.Find(p => p.Category == category).ToList());
        }

        // 譛牙柑縺ｪ繝励Μ繧ｻ繝・ヨ繧偵☆縺ｹ縺ｦ蜿門ｾ・        public async Task<List<UnrealPresetEntry>> GetActiveUnrealPresetsAsync()
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            return await Task.Run(() => presets.Find(p => p.IsActive == true).ToList());
        }

        // UnrealPresetEntry 繧偵・繝ｼ繧ｸ繝ｳ繧ｰ縺ｧ蜿門ｾ・        public async Task<List<UnrealPresetEntry>> GetUnrealPresetEntriesAsync(int skip, int take)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            return await Task.Run(() => presets.Find(Query.All("UpdatedAt", Query.Descending), skip, take).ToList());
        }

        public async IAsyncEnumerable<UnrealPresetEntry> StreamUnrealPresetEntriesAsync(int batchSize = 200)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            int skip = 0;
            while (true)
            {
                var batch = await Task.Run(() => presets.Find(Query.All("UpdatedAt", Query.Descending), skip, batchSize).ToList());
                if (batch.Count == 0) yield break;
                foreach (var item in batch) yield return item;
                skip += batch.Count;
            }
        }

        // UnrealPresetEntry 繧呈峩譁ｰ
        public async Task UpdateUnrealPresetEntryAsync(UnrealPresetEntry presetEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
                var presets = Database.GetCollection<UnrealPresetEntry>();
                await Task.Run(() => presets.Update(presetEntry)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // UnrealPresetEntry 繧貞炎髯､
        public async Task DeleteUnrealPresetEntryAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
                var presets = Database.GetCollection<UnrealPresetEntry>();
                await Task.Run(() => presets.Delete(id)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // Upsert: Name 繧偵く繝ｼ縺ｫ縲∝ｭ伜惠縺吶ｌ縺ｰ譖ｴ譁ｰ縲√↑縺代ｌ縺ｰ菴懈・
        public async Task<UnrealPresetEntry> UpsertUnrealPresetByNameAsync(
            string name,
            string uprojectPath,
            string projectName,
            string? category = null,
            string? description = null,
            string? version = null,
            string? engineVersion = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            return await ExecuteWithWriteLockAsync(async () =>
            {
                var presets = Database.GetCollection<UnrealPresetEntry>();
                return await Task.Run(() =>
                {
                    var existing = presets.FindOne(p => p.Name == name);
                    if (existing != null)
                    {
                        if (!string.IsNullOrEmpty(uprojectPath)) existing.UprojectPath = uprojectPath;
                        if (!string.IsNullOrEmpty(projectName)) existing.ProjectName = projectName;
                        if (category != null) existing.Category = category;
                        if (description != null) existing.Description = description;
                        if (version != null) existing.Version = version;
                        if (engineVersion != null) existing.EngineVersion = engineVersion;
                        existing.UpdatedAt = DateTime.UtcNow;
                        presets.Update(existing);
                        return existing;
                    }

                    var newPreset = new UnrealPresetEntry
                    {
                        Name = name,
                        UprojectPath = uprojectPath,
                        ProjectName = projectName,
                        Category = category ?? string.Empty,
                        Description = description ?? string.Empty,
                        Version = version ?? "1.0.0",
                        EngineVersion = engineVersion ?? string.Empty,
                        IsActive = true,
                        IsCompatible = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    presets.Insert(newPreset);
                    return newPreset;
                }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // PreferenceEntry 縺ｮ霑ｽ蜉縺ｾ縺溘・譖ｴ譁ｰ (Upsert)
        public async Task UpsertPreferenceAsync(string key, string value)
        {
            if (Database == null)
            {
                _logger.LogError("Database is not initialized when calling UpsertPreferenceAsync for key: {Key}", key);
                throw new InvalidOperationException("Database is not initialized");
            }
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogError("Attempted to upsert preference with null or empty key. Value: {Value}", value);
                throw new ArgumentException("Preference key cannot be null or empty.", nameof(key));
            }

            _logger.LogDebug("Upserting preference: Key='{Key}', Value='{Value}'", key, value);

            await ExecuteWithWriteLockAsync(async () =>
            {
                var preferences = Database.GetCollection<PreferenceEntry>();
                try
                {
                    await Task.Run(() => preferences.Upsert(new PreferenceEntry(key, value))).ConfigureAwait(false);
                }
                catch (LiteException lex)
                {
                    _logger.LogWarning(lex, "LiteDB Upsert failed for key '{Key}'. Attempting repair and retry.", key);
                    try
                    {
                        try { Database.GetCollection<PreferenceEntry>().DropIndex("Key"); } catch { }
                        RepairDatabase(preferences);
                    }
                    catch (Exception rex)
                    {
                        _logger.LogError(rex, "RepairDatabase failed while handling Upsert failure for key '{Key}'.", key);
                        throw;
                    }

                    try
                    {
                        await Task.Run(() => preferences.Upsert(new PreferenceEntry(key, value))).ConfigureAwait(false);
                    }
                    catch (Exception retryEx)
                    {
                        _logger.LogError(retryEx, "Retry Upsert failed for key '{Key}'.", key);
                        throw;
                    }
                }
            }).ConfigureAwait(false);

            _logger.LogDebug("Successfully upserted preference: Key='{Key}'", key);
        }

        // PreferenceEntry 繧偵く繝ｼ縺ｧ蜿門ｾ・        public async Task<PreferenceEntry?> GetPreferenceAsync(string key)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var preferences = Database.GetCollection<PreferenceEntry>();

            return await Task.Run(() => preferences.FindById(key));
        }

        // 蜈ｨ莉ｶ蜿門ｾ励・螟ｧ隕乗ｨ｡繝・・繧ｿ縺ｧ髱樊耳螂ｨ縲ゅ・繝ｼ繧ｸ繝ｳ繧ｰAPI縺御ｸ崎ｦ√↑遽・峇縺ｮ縺ｿ縺ｧ菴ｿ逕ｨ縲・        public async Task<List<PreferenceEntry>> GetAllPreferencesAsync(int skip = 0, int take = 200)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var preferences = Database.GetCollection<PreferenceEntry>();
            return await Task.Run(() => preferences.Find(Query.All("_id", Query.Ascending), skip, take).ToList());
        }

        // PreferenceEntry 繧偵く繝ｼ縺ｧ蜑企勁
        public async Task DeletePreferenceAsync(string key)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
                var preferences = Database.GetCollection<PreferenceEntry>();
                await Task.Run(() => preferences.Delete(key)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // ===== ScanCache Collection Methods =====

        /// <summary>
        /// 繧ｹ繧ｭ繝｣繝ｳ繧ｭ繝｣繝・す繝･繧ｨ繝ｳ繝医Μ繧定ｿｽ蜉縺ｾ縺溘・譖ｴ譁ｰ縺励∪縺吶・        /// </summary>
        public async Task UpsertScanCacheAsync(ScanCacheEntry cacheEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            if (cacheEntry == null) throw new ArgumentNullException(nameof(cacheEntry));
            await ExecuteWithWriteLockAsync(async () =>
            {
                var cache = Database.GetCollection<ScanCacheEntry>();
                await Task.Run(() => cache.Upsert(cacheEntry)).ConfigureAwait(false);
                _logger.LogDebug("Upserted scan cache for file: {FilePath}", cacheEntry.FilePath);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 隍・焚繝輔ぃ繧､繝ｫ縺ｮ繝｡繧ｿ繝・・繧ｿ繧偵∪縺ｨ繧√※Upsert縺励∪縺呻ｼ亥腰荳繝ｭ繝・け縺ｧ逶ｴ蛻怜喧・峨・        /// FileEntry 縺ｮ Id 縺ｯ蜀・Κ縺ｧ Path 繧呈､懃ｴ｢縺励※隗｣豎ｺ縺励∪縺吶・        /// </summary>
        public async Task UpsertFilesBatchAsync(IEnumerable<FileEntry> entries)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            if (entries == null) return;
            await ExecuteWithWriteLockAsync(async () =>
            {
                var files = Database.GetCollection<FileEntry>();
                await Task.Run(() =>
                {
                    foreach (var e in entries)
                    {
                        if (e == null || string.IsNullOrWhiteSpace(e.Path)) continue;
                        var existing = files.FindOne(f => f.Path == e.Path);
                        if (existing != null)
                        {
                            e.Id = existing.Id;
                            files.Update(e);
                        }
                        else
                        {
                            files.Insert(e);
                        }
                    }
                }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 隍・焚縺ｮ繧ｹ繧ｭ繝｣繝ｳ繧ｭ繝｣繝・す繝･繧偵∪縺ｨ繧√※Upsert縺励∪縺呻ｼ亥腰荳繝ｭ繝・け・峨・        /// </summary>
        public async Task UpsertScanCacheBatchAsync(IEnumerable<ScanCacheEntry> entries)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            if (entries == null) return;
            await ExecuteWithWriteLockAsync(async () =>
            {
                var cache = Database.GetCollection<ScanCacheEntry>();
                await Task.Run(() =>
                {
                    foreach (var e in entries)
                    {
                        if (e == null || string.IsNullOrWhiteSpace(e.FilePath)) continue;
                        cache.Upsert(e);
                    }
                }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 謖・ｮ壹＆繧後◆繝輔ぃ繧､繝ｫ繝代せ縺ｮ繧ｹ繧ｭ繝｣繝ｳ繧ｭ繝｣繝・す繝･繧貞叙蠕励＠縺ｾ縺吶・        /// </summary>
        public async Task<ScanCacheEntry?> GetScanCacheAsync(string filePath)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            if (string.IsNullOrWhiteSpace(filePath)) return null;

            var cache = Database.GetCollection<ScanCacheEntry>();
            return await Task.Run(() => cache.FindById(filePath));
        }

        /// <summary>
        /// 謖・ｮ壹＆繧後◆繝輔ぃ繧､繝ｫ繝代せ縺ｮ繧ｹ繧ｭ繝｣繝ｳ繧ｭ繝｣繝・す繝･繧貞炎髯､縺励∪縺吶・        /// </summary>
        public async Task DeleteScanCacheAsync(string filePath)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            if (string.IsNullOrWhiteSpace(filePath)) return;
            await ExecuteWithWriteLockAsync(async () =>
            {
                var cache = Database.GetCollection<ScanCacheEntry>();
                await Task.Run(() => cache.Delete(filePath)).ConfigureAwait(false);
                _logger.LogDebug("Deleted scan cache for file: {FilePath}", filePath);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 謖・ｮ壹＆繧後◆繝ｫ繝ｼ繝医ヱ繧ｹ縺ｮ蜈ｨ繧ｹ繧ｭ繝｣繝ｳ繧ｭ繝｣繝・す繝･繧貞叙蠕励＠縺ｾ縺吶・        /// </summary>
        public async Task<List<ScanCacheEntry>> GetScanCacheByRootAsync(string rootPath)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            if (string.IsNullOrWhiteSpace(rootPath)) return new List<ScanCacheEntry>();

            var cache = Database.GetCollection<ScanCacheEntry>();
            return await Task.Run(() => cache.Find(c => c.RootPath == rootPath).ToList());
        }

        /// <summary>
        /// 謖・ｮ壹＆繧後◆繝ｫ繝ｼ繝医ヱ繧ｹ縺ｮ蜈ｨ繧ｹ繧ｭ繝｣繝ｳ繧ｭ繝｣繝・す繝･繧貞炎髯､縺励∪縺吶・        /// </summary>
        public async Task ClearScanCacheByRootAsync(string rootPath)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            if (string.IsNullOrWhiteSpace(rootPath)) return;
            await ExecuteWithWriteLockAsync(async () =>
            {
                var cache = Database.GetCollection<ScanCacheEntry>();
                await Task.Run(() => cache.DeleteMany(c => c.RootPath == rootPath)).ConfigureAwait(false);
                _logger.LogInformation("Cleared all scan cache entries for root path: {RootPath}", rootPath);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 蜈ｨ繧ｹ繧ｭ繝｣繝ｳ繧ｭ繝｣繝・す繝･繧貞炎髯､縺励∪縺吶・        /// </summary>
        public async Task ClearAllScanCacheAsync()
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await ExecuteWithWriteLockAsync(async () =>
            {
                var cache = Database.GetCollection<ScanCacheEntry>();
                await Task.Run(() => cache.DeleteAll()).ConfigureAwait(false);
                _logger.LogInformation("Cleared all scan cache entries.");
            }).ConfigureAwait(false);
        }

        public void Dispose()
        {
            try { Database?.Dispose(); } catch { }
            Database = null;
            try { _dbWriteLock.Dispose(); } catch { }
        }

        // ===== Phase 2.5: Query API Enhancement =====

        #region File Query API

        /// <summary>
        /// 繝輔ぃ繧､繝ｫ繧偵・繝ｭ繝代ユ繧｣縺ｧ讀懃ｴ｢縺励√・繝ｼ繧ｸ繝ｳ繧ｰ蜿門ｾ励＠縺ｾ縺吶・        /// </summary>
        public async Task<List<FileEntry>> GetFilesByPropertyAsync(int skip, int take, string? property = null, string? value = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var files = Database.GetCollection<FileEntry>();
            
            var sw = Stopwatch.StartNew();
            var result = await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(property) || string.IsNullOrWhiteSpace(value))
                {
                    return files.Find(Query.All("Path", Query.Ascending), skip, take).ToList();
                }
                
                // 繧ｷ繝ｳ繝励Ν縺ｪ譁・ｭ怜・繝槭ャ繝√Φ繧ｰ・医・繝ｭ繝代ユ繧｣蛻･・・                if (property.Equals("Type", StringComparison.OrdinalIgnoreCase))
                {
                    return files.Find(f => f.Type == value, skip, take).ToList();
                }
                else if (property.Equals("Path", StringComparison.OrdinalIgnoreCase))
                {
                    return files.Find(f => f.Path.Contains(value), skip, take).ToList();
                }
                
                return files.Find(Query.All("Path", Query.Ascending), skip, take).ToList();
            }).ConfigureAwait(false);
            sw.Stop();
            _logger.LogDebug("GetFilesByPropertyAsync property={Property} value={Value} skip={Skip} take={Take} fetched={Count} in {Ms} ms", property, value, skip, take, result.Count, sw.ElapsedMilliseconds);
            return result;
        }

        /// <summary>
        /// 繝輔ぃ繧､繝ｫ邱乗焚繧貞叙蠕励＠縺ｾ縺呻ｼ医が繝励す繝ｧ繝ｳ譚｡莉ｶ莉倥″・峨・        /// </summary>
        public async Task<int> GetFileCountAsync(string? property = null, string? value = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var files = Database.GetCollection<FileEntry>();
            
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(property) || string.IsNullOrWhiteSpace(value))
                {
                    return files.Count();
                }
                
                if (property.Equals("Type", StringComparison.OrdinalIgnoreCase))
                {
                    return files.Count(f => f.Type == value);
                }
                else if (property.Equals("Path", StringComparison.OrdinalIgnoreCase))
                {
                    return files.Count(f => f.Path.Contains(value));
                }
                
                return files.Count();
            }).ConfigureAwait(false);
        }

        #endregion

        #region Image Query API

        /// <summary>
        /// 逕ｻ蜒上ｒ繝励Ο繝代ユ繧｣縺ｧ讀懃ｴ｢縺励√・繝ｼ繧ｸ繝ｳ繧ｰ蜿門ｾ励＠縺ｾ縺吶・        /// </summary>
        public async Task<List<ImageEntry>> GetImagesByPropertyAsync(int skip, int take, string? category = null, string? colorSpace = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var images = Database.GetCollection<ImageEntry>();
            
            var sw = Stopwatch.StartNew();
            var result = await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(category) && string.IsNullOrWhiteSpace(colorSpace))
                {
                    return images.Include(i => i.File).Find(Query.All(), skip, take).ToList();
                }
                
                // 繧ｫ繝・ざ繝ｪ縺ｾ縺溘・繧ｫ繝ｩ繝ｼ遨ｺ髢薙〒繝輔ぅ繝ｫ繧ｿ繝ｪ繝ｳ繧ｰ
                if (!string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(colorSpace))
                {
                    // 荳｡譚｡莉ｶ繝槭ャ繝・                    return images.Include(i => i.File).Find(Query.All(), skip, take).ToList(); // LiteDB 縺ｧ縺ｯ隍・尅縺ｪ譚｡莉ｶ縺ｧ縺ｮ Find 縺ｯ蝗ｰ髮｣縺ｪ縺溘ａ縲√Γ繝｢繝ｪ蜀・ヵ繧｣繝ｫ繧ｿ繝ｪ繝ｳ繧ｰ
                }
                else if (!string.IsNullOrWhiteSpace(category))
                {
                    return images.Include(i => i.File).Find(Query.All(), skip, take).ToList();
                }
                else
                {
                    return images.Include(i => i.File).Find(Query.All(), skip, take).ToList();
                }
            }).ConfigureAwait(false);
            sw.Stop();
            _logger.LogDebug("GetImagesByPropertyAsync category={Category} colorSpace={ColorSpace} skip={Skip} take={Take} fetched={Count} in {Ms} ms", category, colorSpace, skip, take, result.Count, sw.ElapsedMilliseconds);
            return result;
        }

        /// <summary>
        /// 逕ｻ蜒冗ｷ乗焚繧貞叙蠕励＠縺ｾ縺吶・        /// </summary>
        public async Task<int> GetImageCountAsync(string? category = null, string? colorSpace = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var images = Database.GetCollection<ImageEntry>();
            
            return await Task.Run(() => images.Count()).ConfigureAwait(false);
        }

        #endregion

        #region Asset Query API

        /// <summary>
        /// 繧｢繧ｻ繝・ヨ繧偵ち繧､繝怜挨縺ｧ讀懃ｴ｢縺励√・繝ｼ繧ｸ繝ｳ繧ｰ蜿門ｾ励＠縺ｾ縺吶・        /// </summary>
        public async Task<List<AssetEntry>> GetAssetsByTypeAsync(string type, int skip, int take)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            if (string.IsNullOrWhiteSpace(type)) return new List<AssetEntry>();
            
            var assets = Database.GetCollection<AssetEntry>();
            var sw = Stopwatch.StartNew();
            var result = await Task.Run(() => assets.Include(a => a.File).Find(a => a.Type == type, skip, take).ToList()).ConfigureAwait(false);
            sw.Stop();
            _logger.LogDebug("GetAssetsByTypeAsync type={Type} skip={Skip} take={Take} fetched={Count} in {Ms} ms", type, skip, take, result.Count, sw.ElapsedMilliseconds);
            return result;
        }

        /// <summary>
        /// 繧｢繧ｻ繝・ヨ繧偵・繝ｭ繝代ユ繧｣縺ｧ讀懃ｴ｢縺励√・繝ｼ繧ｸ繝ｳ繧ｰ蜿門ｾ励＠縺ｾ縺吶・        /// </summary>
        public async Task<List<AssetEntry>> GetAssetsByPropertyAsync(int skip, int take, string? type = null, string? category = null, long? maxSize = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var assets = Database.GetCollection<AssetEntry>();
            
            var sw = Stopwatch.StartNew();
            var result = await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(type) && string.IsNullOrWhiteSpace(category) && !maxSize.HasValue)
                {
                    return assets.Include(a => a.File).Find(Query.All("UpdatedAt", Query.Descending), skip, take).ToList();
                }
                
                // 譚｡莉ｶ繝薙Ν繝繝ｼ
                if (!string.IsNullOrWhiteSpace(type))
                {
                    return assets.Include(a => a.File).Find(a => a.Type == type, skip, take).ToList();
                }
                
                return assets.Include(a => a.File).Find(Query.All("UpdatedAt", Query.Descending), skip, take).ToList();
            }).ConfigureAwait(false);
            sw.Stop();
            _logger.LogDebug("GetAssetsByPropertyAsync type={Type} category={Category} maxSize={MaxSize} skip={Skip} take={Take} fetched={Count} in {Ms} ms", type, category, maxSize, skip, take, result.Count, sw.ElapsedMilliseconds);
            return result;
        }

        /// <summary>
        /// 繧｢繧ｻ繝・ヨ邱乗焚繧貞叙蠕励＠縺ｾ縺吶・        /// </summary>
        public async Task<int> GetAssetCountAsync(string? type = null, string? category = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var assets = Database.GetCollection<AssetEntry>();
            
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(type))
                {
                    return assets.Count();
                }
                return assets.Count(a => a.Type == type);
            }).ConfigureAwait(false);
        }

        #endregion

        #region Project Query API

        /// <summary>
        /// 繝励Ο繧ｸ繧ｧ繧ｯ繝医ｒ繝励Ο繝代ユ繧｣縺ｧ讀懃ｴ｢縺励√・繝ｼ繧ｸ繝ｳ繧ｰ蜿門ｾ励＠縺ｾ縺吶・        /// </summary>
        public async Task<List<ProjectEntry>> GetProjectsByPropertyAsync(int skip, int take, string? name = null, string? status = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var projects = Database.GetCollection<ProjectEntry>();
            
            var sw = Stopwatch.StartNew();
            var result = await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(status))
                {
                    return projects.Include(p => p.Files).Find(Query.All("UpdatedAt", Query.Descending), skip, take).ToList();
                }
                
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return projects.Include(p => p.Files).Find(p => p.Name.Contains(name), skip, take).ToList();
                }
                
                return projects.Include(p => p.Files).Find(Query.All("UpdatedAt", Query.Descending), skip, take).ToList();
            }).ConfigureAwait(false);
            sw.Stop();
            _logger.LogDebug("GetProjectsByPropertyAsync name={Name} status={Status} skip={Skip} take={Take} fetched={Count} in {Ms} ms", name, status, skip, take, result.Count, sw.ElapsedMilliseconds);
            return result;
        }

        /// <summary>
        /// 繝励Ο繧ｸ繧ｧ繧ｯ繝育ｷ乗焚繧貞叙蠕励＠縺ｾ縺吶・        /// </summary>
        public async Task<int> GetProjectCountAsync(string? status = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var projects = Database.GetCollection<ProjectEntry>();
            
            return await Task.Run(() => projects.Count()).ConfigureAwait(false);
        }

        #endregion

        #region Script Query API

        /// <summary>
        /// 繧ｹ繧ｯ繝ｪ繝励ヨ繧偵・繝ｭ繝代ユ繧｣縺ｧ讀懃ｴ｢縺励√・繝ｼ繧ｸ繝ｳ繧ｰ蜿門ｾ励＠縺ｾ縺吶・        /// </summary>
        public async Task<List<ScriptEntry>> GetScriptsByPropertyAsync(int skip, int take, string? language = null, string? application = null, string? category = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            
            var sw = Stopwatch.StartNew();
            var result = await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(language) && string.IsNullOrWhiteSpace(application) && string.IsNullOrWhiteSpace(category))
                {
                    return scripts.Find(Query.All("UpdatedAt", Query.Descending), skip, take).ToList();
                }
                
                if (!string.IsNullOrWhiteSpace(language))
                {
                    return scripts.Find(s => s.Language == language, skip, take).ToList();
                }
                
                return scripts.Find(Query.All("UpdatedAt", Query.Descending), skip, take).ToList();
            }).ConfigureAwait(false);
            sw.Stop();
            _logger.LogDebug("GetScriptsByPropertyAsync language={Language} application={Application} category={Category} skip={Skip} take={Take} fetched={Count} in {Ms} ms", language, application, category, skip, take, result.Count, sw.ElapsedMilliseconds);
            return result;
        }

        /// <summary>
        /// 繧ｹ繧ｯ繝ｪ繝励ヨ邱乗焚繧貞叙蠕励＠縺ｾ縺吶・        /// </summary>
        public async Task<int> GetScriptCountAsync(string? language = null, string? application = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(language))
                {
                    return scripts.Count();
                }
                return scripts.Count(s => s.Language == language);
            }).ConfigureAwait(false);
        }

        #endregion

        #region UnrealPreset Query API

        /// <summary>
        /// UnrealPreset繧偵・繝ｭ繝代ユ繧｣縺ｧ讀懃ｴ｢縺励√・繝ｼ繧ｸ繝ｳ繧ｰ蜿門ｾ励＠縺ｾ縺吶・        /// </summary>
        public async Task<List<UnrealPresetEntry>> GetUnrealPresetsByPropertyAsync(int skip, int take, string? projectName = null, string? category = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            
            var sw = Stopwatch.StartNew();
            var result = await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(projectName) && string.IsNullOrWhiteSpace(category))
                {
                    return presets.Find(Query.All("UpdatedAt", Query.Descending), skip, take).ToList();
                }
                
                if (!string.IsNullOrWhiteSpace(projectName))
                {
                    return presets.Find(p => p.ProjectName == projectName, skip, take).ToList();
                }
                
                return presets.Find(Query.All("UpdatedAt", Query.Descending), skip, take).ToList();
            }).ConfigureAwait(false);
            sw.Stop();
            _logger.LogDebug("GetUnrealPresetsByPropertyAsync projectName={ProjectName} category={Category} skip={Skip} take={Take} fetched={Count} in {Ms} ms", projectName, category, skip, take, result.Count, sw.ElapsedMilliseconds);
            return result;
        }

        /// <summary>
        /// UnrealPreset邱乗焚繧貞叙蠕励＠縺ｾ縺吶・        /// </summary>
        public async Task<int> GetUnrealPresetCountAsync(string? projectName = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(projectName))
                {
                    return presets.Count();
                }
                return presets.Count(p => p.ProjectName == projectName);
            }).ConfigureAwait(false);
        }

        #endregion

        // TODO: Bridge騾壻ｿ｡逕ｨ繝｡繧ｽ繝・ラ・亥ｾ後〒螳溯｣・ｺ亥ｮ夲ｼ・        // - 繝励Μ繧ｻ繝・ヨ蜷梧悄繝｡繧ｽ繝・ラ・・nreal <-> Pivot・・        // - 繝励Μ繧ｻ繝・ヨ驕ｩ逕ｨ繝｡繧ｽ繝・ラ・・nreal蜀・〒縺ｮ驕ｩ逕ｨ蜃ｦ逅・ｼ・        // - 萓晏ｭ倬未菫りｧ｣豎ｺ繝｡繧ｽ繝・ラ・医・繝ｪ繧ｻ繝・ヨ髢薙・萓晏ｭ倬未菫ゅｒ讀懆ｨｼ・・        // - 繝励Μ繧ｻ繝・ヨ讀懆ｨｼ繝｡繧ｽ繝・ラ・井ｺ呈鋤諤ｧ繝√ぉ繝・け・・
        /// <summary>
        /// 繧｢繧ｻ繝・ヨ繧偵ョ繧｣繝ｬ繧ｯ繝医Μ縺ｧ讀懃ｴ｢縺励√・繝ｼ繧ｸ繝ｳ繧ｰ蜿門ｾ励＠縺ｾ縺吶・        /// </summary>
        public async Task<List<AssetEntry>> GetAssetsByDirectoryAsync(string directoryPath, int skip, int take)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            if (string.IsNullOrWhiteSpace(directoryPath)) return new List<AssetEntry>();

            var assets = Database.GetCollection<AssetEntry>();
            var sw = Stopwatch.StartNew();
            var result = await Task.Run(() => assets.Include(a => a.File).Find(a => a.Path.StartsWith(directoryPath), skip, take).ToList()).ConfigureAwait(false);
            sw.Stop();
            _logger.LogDebug("GetAssetsByDirectoryAsync directoryPath={DirectoryPath} skip={Skip} take={Take} fetched={Count} in {Ms} ms", directoryPath, skip, take, result.Count, sw.ElapsedMilliseconds);
            return result;
        }

        /// <summary>
        /// 繝・ぅ繝ｬ繧ｯ繝医Μ縺ｫ髢｢騾｣縺吶ｋ繧｢繧ｻ繝・ヨ邱乗焚繧貞叙蠕励＠縺ｾ縺吶・        /// </summary>
        public async Task<int> GetAssetCountByDirectoryAsync(string directoryPath)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            if (string.IsNullOrWhiteSpace(directoryPath)) return 0;

            var assets = Database.GetCollection<AssetEntry>();
            return await Task.Run(() => assets.Count(a => a.Path.StartsWith(directoryPath))).ConfigureAwait(false);
        }
        /// <summary>
        /// 隍・焚縺ｮ繝・ぅ繝ｬ繧ｯ繝医Μ驟堺ｸ九・繧｢繧ｻ繝・ヨ繧偵・繝ｼ繧ｸ繝ｳ繧ｰ蜿門ｾ励＠縺ｾ縺呻ｼ域怙驕ｩ蛹也沿・峨・        /// </summary>
        public async Task<List<AssetEntry>> GetAssetsByDirectoriesAsync(int skip, int take, List<string> directories, string? type = null, string? category = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            if (directories == null || directories.Count == 0) return new List<AssetEntry>();

            var assets = Database.GetCollection<AssetEntry>();
            var sw = Stopwatch.StartNew();

            var result = await Task.Run(() =>
            {
                IEnumerable<AssetEntry> query = assets.Include(a => a.File).FindAll()
                    .Where(a => directories.Any(dir => a.Path.StartsWith(dir, StringComparison.OrdinalIgnoreCase)));

                if (!string.IsNullOrWhiteSpace(type))
                {
                    query = query.Where(a => a.Type == type);
                }

                query = query.OrderByDescending(a => a.UpdatedAt).Skip(skip).Take(take);

                return query.ToList();
            }).ConfigureAwait(false);

            sw.Stop();
            _logger.LogDebug("GetAssetsByDirectoriesAsync directories={DirCount} type={Type} skip={Skip} take={Take} fetched={Count} in {Ms} ms", directories.Count, type, skip, take, result.Count, sw.ElapsedMilliseconds);
            return result;
        }

        /// <summary>
        /// 隍・焚縺ｮ繝・ぅ繝ｬ繧ｯ繝医Μ驟堺ｸ九・繧｢繧ｻ繝・ヨ邱乗焚繧貞叙蠕励＠縺ｾ縺呻ｼ域怙驕ｩ蛹也沿・峨・        /// </summary>
        public async Task<int> GetAssetCountByDirectoriesAsync(List<string> directories, string? type = null, string? category = null)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            if (directories == null || directories.Count == 0) return 0;

            var assets = Database.GetCollection<AssetEntry>();

            return await Task.Run(() =>
            {
                var query = assets.FindAll()
                    .Where(a => directories.Any(dir => a.Path.StartsWith(dir, StringComparison.OrdinalIgnoreCase)));

                if (!string.IsNullOrWhiteSpace(type))
                {
                    query = query.Where(a => a.Type == type);
                }

                return query.Count();
            }).ConfigureAwait(false);
        }
    }
}

