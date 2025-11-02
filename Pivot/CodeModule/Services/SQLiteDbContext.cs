using Microsoft.EntityFrameworkCore;
using Pivot.CodeModule.Models;

namespace Pivot.CodeModule.Services
{
    public class SQLiteDbContext : DbContext
    {
        public DbSet<CodeFile> CodeFiles { get; set; } = null!;

        public DbSet<CodeTag> Tags { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // Simple default; DI registration can override this with a proper local appdata path
            options.UseSqlite("Data Source=codehub.db");
        }
    }
}


