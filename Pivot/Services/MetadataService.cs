using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using Pivot.Models; // 必要に応じてモデルを定義する
using LiteDB; // LiteDB を使用するために追加

namespace Pivot.Services
{
    public class MetadataService
    {
        private readonly ILogger<MetadataService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _databasePath;

        public LiteDatabase? Database { get; private set; }

        public MetadataService(ILogger<MetadataService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _databasePath = Path.Combine(AppContext.BaseDirectory, _configuration["AppSettings:Database:ConnectionString"]?.Replace("Data Source=", "") ?? "pivot.db");

            _logger.LogInformation($"Database path: {_databasePath}");

            // LiteDB の初期化 (Database オブジェクトを作成)
            // InitializeDatabase().Wait(); // InitializeDatabaseで一元的に初期化
        }

        public async Task InitializeDatabase()
        {
            if (Database != null) return;

            try
            {
                Database = new LiteDatabase(_databasePath);

                var files = Database.GetCollection<FileEntry>();
                files.EnsureIndex(f => f.Path, true); // Path はユニークインデックス
                files.EnsureIndex(f => f.Hash, false); // Hash は非ユニークインデックス

                var images = Database.GetCollection<ImageEntry>();
                images.EnsureIndex(i => i.File.Id); // FileEntry への参照インデックス

                // ProjectEntry のコレクションも初期化
                var projects = Database.GetCollection<ProjectEntry>();
                projects.EnsureIndex(p => p.Name, true); // Name はユニークインデックス

                // 画像関連のコレクションも初期化
                images.EnsureIndex(i => i.Width);
                images.EnsureIndex(i => i.Height);
                // Assets コレクション初期化
                var assets = Database.GetCollection<AssetEntry>();
                // Use string field names to avoid ambiguous member resolution in expression trees
                assets.EnsureIndex("Path", true);
                assets.EnsureIndex("Hash", false);

                // Scripts コレクション初期化
                var scripts = Database.GetCollection<ScriptEntry>();
                scripts.EnsureIndex("Path", true);  // Path はユニークインデックス
                scripts.EnsureIndex("Language", false);  // Language は非ユニークインデックス
                scripts.EnsureIndex("TargetApplication", false);  // TargetApplication は非ユニークインデックス
                scripts.EnsureIndex("Category", false);  // Category は非ユニークインデックス

                // UnrealPresets コレクション初期化
                var presets = Database.GetCollection<UnrealPresetEntry>();
                presets.EnsureIndex("Name", false);  // Name は非ユニークインデックス
                presets.EnsureIndex("UprojectPath", false);  // UprojectPath は非ユニークインデックス
                presets.EnsureIndex("Category", false);  // Category は非ユニークインデックス
                presets.EnsureIndex("ProjectName", false);  // ProjectName は非ユニークインデックス

                _logger.LogInformation("Database initialized and tables created.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize database.");
            }
        }

        public async Task UpsertFileAsync(string path, string type, long size, DateTime updatedAt, string hash)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");

            var files = Database.GetCollection<FileEntry>();
            var newEntry = new FileEntry
            {
                Path = path,
                Type = type,
                Size = size,
                UpdatedAt = updatedAt,
                Hash = hash
            };
            
            // LiteDB の Upsert は Id を基準に行われるため、Path で検索して Id を設定する
            var existingEntry = files.FindOne(f => f.Path == path);
            if (existingEntry != null)
            {
                newEntry.Id = existingEntry.Id; // 既存のIdを再利用
                files.Update(newEntry); // 更新
            }
            else
            {
                files.Insert(newEntry); // 新規挿入
            }
        }

        public async Task DeleteFileAsync(string path)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var files = Database.GetCollection<FileEntry>();
            var matches = files.Find(Query.EQ("Path", path)).ToList();
            foreach (var m in matches)
            {
                files.Delete(m.Id);
            }
        }

        public async Task<List<FileEntry>> GetFilesAsync(int skip, int take)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var files = Database.GetCollection<FileEntry>();
            return await Task.Run(() => files.Find(Query.All("UpdatedAt", Query.Descending), skip, take).ToList());
        }

        // FileEntry を ID で取得
        public async Task<FileEntry?> GetFileEntryByIdAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var files = Database.GetCollection<FileEntry>();
            return await Task.Run(() => files.FindById(id));
        }

        // FileEntry を Path で取得
        public async Task<FileEntry?> GetFileEntryByPathAsync(string path)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var files = Database.GetCollection<FileEntry>();
            return await Task.Run(() => files.FindOne(f => f.Path == path));
        }

        // FileEntry を更新
        public async Task UpdateFileEntryAsync(FileEntry fileEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var files = Database.GetCollection<FileEntry>();
            await Task.Run(() => files.Update(fileEntry));
        }

        // FileEntry を削除
        public async Task DeleteFileEntryAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var files = Database.GetCollection<FileEntry>();
            await Task.Run(() => files.Delete(id));
        }

        // ImageEntry の追加
        public async Task AddImageEntryAsync(ImageEntry imageEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var images = Database.GetCollection<ImageEntry>();
            await Task.Run(() => images.Insert(imageEntry));
        }

        // ImageEntry を ID で取得
        public async Task<ImageEntry?> GetImageEntryByIdAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var images = Database.GetCollection<ImageEntry>();
            return await Task.Run(() => images.Include(i => i.File).FindById(id));
        }

        // FileEntry の ID に紐づく ImageEntry を取得
        public async Task<ImageEntry?> GetImageEntryByFileIdAsync(int fileId)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var images = Database.GetCollection<ImageEntry>();
            return await Task.Run(() => images.Include(i => i.File).FindOne(i => i.File.Id == fileId));
        }

        // ImageEntry を更新
        public async Task UpdateImageEntryAsync(ImageEntry imageEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var images = Database.GetCollection<ImageEntry>();
            await Task.Run(() => images.Update(imageEntry));
        }

        // ImageEntry を削除
        public async Task DeleteImageEntryAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var images = Database.GetCollection<ImageEntry>();
            await Task.Run(() => images.Delete(id));
        }

        // 全ての ImageEntry を取得 (ページングなし)
        public async Task<List<ImageEntry>> GetAllImageEntriesAsync()
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var images = Database.GetCollection<ImageEntry>();
            return await Task.Run(() => images.Include(i => i.File).FindAll().ToList());
        }

        // ImageEntry をページングで取得
        public async Task<List<ImageEntry>> GetImageEntriesAsync(int skip, int take)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var images = Database.GetCollection<ImageEntry>();
            return await Task.Run(() => images.Include(i => i.File).Find(Query.All(), skip, take).ToList());
        }

        // ProjectEntry の追加
        public async Task AddProjectEntryAsync(ProjectEntry projectEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var projects = Database.GetCollection<ProjectEntry>();
            await Task.Run(() => projects.Insert(projectEntry));
        }

        // ProjectEntry を ID で取得
        public async Task<ProjectEntry?> GetProjectEntryByIdAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var projects = Database.GetCollection<ProjectEntry>();
            return await Task.Run(() => projects.Include(p => p.Files).FindById(id));
        }

        // ProjectEntry を Name で取得
        public async Task<ProjectEntry?> GetProjectEntryByNameAsync(string name)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var projects = Database.GetCollection<ProjectEntry>();
            return await Task.Run(() => projects.Include(p => p.Files).FindOne(p => p.Name == name));
        }

        // ProjectEntry を更新
        public async Task UpdateProjectEntryAsync(ProjectEntry projectEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var projects = Database.GetCollection<ProjectEntry>();
            await Task.Run(() => projects.Update(projectEntry));
        }

        // ProjectEntry を削除
        public async Task DeleteProjectEntryAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var projects = Database.GetCollection<ProjectEntry>();
            await Task.Run(() => projects.Delete(id));
        }

        // 全ての ProjectEntry を取得 (ページングなし)
        public async Task<List<ProjectEntry>> GetAllProjectEntriesAsync()
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var projects = Database.GetCollection<ProjectEntry>();
            return await Task.Run(() => projects.Include(p => p.Files).FindAll().ToList());
        }

        // ProjectEntry をページングで取得
        public async Task<List<ProjectEntry>> GetProjectEntriesAsync(int skip, int take)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var projects = Database.GetCollection<ProjectEntry>();
            return await Task.Run(() => projects
                .Include(p => p.Files)
                .Find(Query.All("UpdatedAt", Query.Descending), skip, take)
                .ToList());
        }

        // Name をキーとして Upsert（存在すれば更新、なければ作成）
        public async Task<ProjectEntry> UpsertProjectByNameAsync(string name, string? description = null, string? path = null)
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

        // AssetEntry の追加
        public async Task AddAssetEntryAsync(AssetEntry assetEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var assets = Database.GetCollection<AssetEntry>();
            await Task.Run(() => assets.Insert(assetEntry));
        }

        // AssetEntry を ID で取得
        public async Task<AssetEntry?> GetAssetEntryByIdAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var assets = Database.GetCollection<AssetEntry>();
            return await Task.Run(() => assets.Include(a => a.File).FindById(id));
        }

        // Path による取得
        public async Task<AssetEntry?> GetAssetEntryByPathAsync(string path)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var assets = Database.GetCollection<AssetEntry>();
            return await Task.Run(() => assets.Include(a => a.File).FindOne(a => a.Path == path));
        }

        // AssetEntry をページングで取得
        public async Task<List<AssetEntry>> GetAssetEntriesAsync(int skip, int take)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var assets = Database.GetCollection<AssetEntry>();
            return await Task.Run(() => assets.Include(a => a.File).Find(Query.All("UpdatedAt", Query.Descending), skip, take).ToList());
        }

        // AssetEntry を更新
        public async Task UpdateAssetEntryAsync(AssetEntry assetEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var assets = Database.GetCollection<AssetEntry>();
            await Task.Run(() => assets.Update(assetEntry));
        }

        // AssetEntry を削除
        public async Task DeleteAssetEntryAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var assets = Database.GetCollection<AssetEntry>();
            await Task.Run(() => assets.Delete(id));
        }

        // ScriptEntry の追加
        public async Task AddScriptEntryAsync(ScriptEntry scriptEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            await Task.Run(() => scripts.Insert(scriptEntry));
        }

        // ScriptEntry を ID で取得
        public async Task<ScriptEntry?> GetScriptEntryByIdAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            return await Task.Run(() => scripts.FindById(id));
        }

        // ScriptEntry を Path で取得
        public async Task<ScriptEntry?> GetScriptEntryByPathAsync(string path)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            return await Task.Run(() => scripts.FindOne(s => s.Path == path));
        }

        // ScriptEntry を Name で取得
        public async Task<ScriptEntry?> GetScriptEntryByNameAsync(string name)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            return await Task.Run(() => scripts.FindOne(s => s.Name == name));
        }

        // 特定の言語のスクリプトをすべて取得
        public async Task<List<ScriptEntry>> GetScriptsByLanguageAsync(string language)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            return await Task.Run(() => scripts.Find(s => s.Language == language).ToList());
        }

        // 特定のアプリケーション対応のスクリプトをすべて取得
        public async Task<List<ScriptEntry>> GetScriptsByApplicationAsync(string targetApplication)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            return await Task.Run(() => scripts.Find(s => s.TargetApplication == targetApplication).ToList());
        }

        // 特定のカテゴリのスクリプトをすべて取得
        public async Task<List<ScriptEntry>> GetScriptsByCategoryAsync(string category)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            return await Task.Run(() => scripts.Find(s => s.Category == category).ToList());
        }

        // 有効なスクリプトをすべて取得
        public async Task<List<ScriptEntry>> GetActiveScriptsAsync()
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            return await Task.Run(() => scripts.Find(s => s.IsActive == true).ToList());
        }

        // ScriptEntry をページングで取得
        public async Task<List<ScriptEntry>> GetScriptEntriesAsync(int skip, int take)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            return await Task.Run(() => scripts.Find(Query.All("UpdatedAt", Query.Descending), skip, take).ToList());
        }

        // ScriptEntry を更新
        public async Task UpdateScriptEntryAsync(ScriptEntry scriptEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            await Task.Run(() => scripts.Update(scriptEntry));
        }

        // ScriptEntry を削除
        public async Task DeleteScriptEntryAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            await Task.Run(() => scripts.Delete(id));
        }

        // Path で削除
        public async Task DeleteScriptByPathAsync(string path)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var scripts = Database.GetCollection<ScriptEntry>();
            var script = await GetScriptEntryByPathAsync(path);
            if (script != null)
            {
                await DeleteScriptEntryAsync(script.Id);
            }
        }

        // Upsert: Path をキーに、存在すれば更新、なければ作成
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
            var scripts = Database.GetCollection<ScriptEntry>();

            return await Task.Run(() =>
            {
                var existing = scripts.FindOne(s => s.Path == path);
                if (existing != null)
                {
                    // 既存エントリを更新
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

                // 新規作成
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
            });
        }

        // UnrealPresetEntry の追加
        public async Task AddUnrealPresetEntryAsync(UnrealPresetEntry presetEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            await Task.Run(() => presets.Insert(presetEntry));
        }

        // UnrealPresetEntry を ID で取得
        public async Task<UnrealPresetEntry?> GetUnrealPresetEntryByIdAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            return await Task.Run(() => presets.FindById(id));
        }

        // UnrealPresetEntry を Name で取得
        public async Task<UnrealPresetEntry?> GetUnrealPresetEntryByNameAsync(string name)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            return await Task.Run(() => presets.FindOne(p => p.Name == name));
        }

        // 特定の .uproject に関連するプリセットをすべて取得
        public async Task<List<UnrealPresetEntry>> GetPresetsByUprojectAsync(string uprojectPath)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            return await Task.Run(() => presets.Find(p => p.UprojectPath == uprojectPath).ToList());
        }

        // 特定のカテゴリのプリセットをすべて取得
        public async Task<List<UnrealPresetEntry>> GetPresetsByCategoryAsync(string category)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            return await Task.Run(() => presets.Find(p => p.Category == category).ToList());
        }

        // 有効なプリセットをすべて取得
        public async Task<List<UnrealPresetEntry>> GetActiveUnrealPresetsAsync()
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            return await Task.Run(() => presets.Find(p => p.IsActive == true).ToList());
        }

        // UnrealPresetEntry をページングで取得
        public async Task<List<UnrealPresetEntry>> GetUnrealPresetEntriesAsync(int skip, int take)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            return await Task.Run(() => presets.Find(Query.All("UpdatedAt", Query.Descending), skip, take).ToList());
        }

        // UnrealPresetEntry を更新
        public async Task UpdateUnrealPresetEntryAsync(UnrealPresetEntry presetEntry)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            await Task.Run(() => presets.Update(presetEntry));
        }

        // UnrealPresetEntry を削除
        public async Task DeleteUnrealPresetEntryAsync(int id)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            var presets = Database.GetCollection<UnrealPresetEntry>();
            await Task.Run(() => presets.Delete(id));
        }

        // Upsert: Name をキーに、存在すれば更新、なければ作成
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
            var presets = Database.GetCollection<UnrealPresetEntry>();

            return await Task.Run(() =>
            {
                var existing = presets.FindOne(p => p.Name == name);
                if (existing != null)
                {
                    // 既存エントリを更新
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

                // 新規作成
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
            });
        }

        // TODO: Bridge通信用メソッド（後で実装予定）
        // - プリセット同期メソッド（Unreal <-> Pivot）
        // - プリセット適用メソッド（Unreal内での適用処理）
        // - 依存関係解決メソッド（プリセット間の依存関係を検証）
        // - プリセット検証メソッド（互換性チェック）
    }
}
