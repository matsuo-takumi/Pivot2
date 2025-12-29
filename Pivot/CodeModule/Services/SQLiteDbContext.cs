using Microsoft.EntityFrameworkCore;
using Pivot.CodeModule.Models;
using Pivot.Models;

namespace Pivot.CodeModule.Services
{
    public class SQLiteDbContext : DbContext
    {
        public DbSet<CodeFile> CodeFiles { get; set; } = null!;

        public DbSet<CodeTag> Tags { get; set; } = null!;

        /// <summary>
        /// アセットファイル（画像、3Dモデル等）のメタデータ
        /// </summary>
        public DbSet<AssetFile> AssetFiles { get; set; } = null!;

        // Parameterless constructor kept for compatibility, but provide an options-taking ctor
        public SQLiteDbContext()
        {
        }

        public SQLiteDbContext(DbContextOptions<SQLiteDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // Simple default; DI registration can override this with a proper local appdata path
            if (!options.IsConfigured)
            {
                options.UseSqlite("Data Source=codehub.db");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // AssetFiles indexing for fast queries
            modelBuilder.Entity<AssetFile>(entity =>
            {
                entity.HasIndex(e => e.FilePath).IsUnique();
                entity.HasIndex(e => e.Directory);
                entity.HasIndex(e => e.Kind);
                entity.HasIndex(e => e.IsDeleted);
            });
        }
    }
}



