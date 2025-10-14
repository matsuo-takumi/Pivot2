using SQLite;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using Pivot.Models; // 必要に応じてモデルを定義する

namespace Pivot.Services
{
    public class MetadataService
    {
        private readonly ILogger<MetadataService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _databasePath;

        public SQLiteAsyncConnection? Database { get; private set; }

        public MetadataService(ILogger<MetadataService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _databasePath = Path.Combine(AppContext.BaseDirectory, _configuration["AppSettings:Database:ConnectionString"]?.Replace("Data Source=", "") ?? "pivot.db");

            _logger.LogInformation($"Database path: {_databasePath}");

            // InitializeDatabase().Wait(); // アプリケーション起動時にDBを初期化するため同期的に待機
        }

        public async Task InitializeDatabase()
        {
            if (Database != null) return;

            try
            {
                Database = new SQLiteAsyncConnection(_databasePath);
                await Database.CreateTableAsync<FileEntry>(CreateFlags.None);
                await Database.CreateTableAsync<ImageEntry>(CreateFlags.None);

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

            // ハッシュ重複管理: 既存があれば更新、なければ挿入
            var existingByHash = await Database.Table<FileEntry>().Where(f => f.Hash == hash).FirstOrDefaultAsync();
            if (existingByHash != null)
            {
                existingByHash.Path = path;
                existingByHash.Type = type;
                existingByHash.Size = size;
                existingByHash.UpdatedAt = updatedAt;
                await Database.UpdateAsync(existingByHash);
                return;
            }

            // パス一致での更新（ハッシュ変更等）
            var existingByPath = await Database.Table<FileEntry>().Where(f => f.Path == path).FirstOrDefaultAsync();
            if (existingByPath != null)
            {
                existingByPath.Type = type;
                existingByPath.Size = size;
                existingByPath.UpdatedAt = updatedAt;
                existingByPath.Hash = hash;
                await Database.UpdateAsync(existingByPath);
                return;
            }

            var newEntry = new FileEntry
            {
                Path = path,
                Type = type,
                Size = size,
                UpdatedAt = updatedAt,
                Hash = hash
            };
            await Database.InsertAsync(newEntry);
        }

        public async Task DeleteFileAsync(string path)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            await Database.Table<FileEntry>().DeleteAsync(f => f.Path == path);
        }

        public async Task<List<FileEntry>> GetFilesAsync(int skip, int take)
        {
            if (Database == null) throw new InvalidOperationException("Database is not initialized");
            return await Database.Table<FileEntry>()
                .OrderByDescending(f => f.UpdatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }
    }
}
