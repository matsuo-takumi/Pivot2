using Microsoft.EntityFrameworkCore;
using Pivot.CodeModule.Models;

namespace Pivot.CodeModule.Services
{
    public class SQLiteDbContext : DbContext
    {
        public DbSet<CodeFile> CodeFiles { get; set; } = null!;

        public DbSet<CodeTag> Tags { get; set; } = null!;

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
    }
}


