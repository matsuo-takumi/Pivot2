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
    }
}
