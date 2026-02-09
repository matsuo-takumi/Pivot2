using Microsoft.EntityFrameworkCore;
using Pivot.Engine.Models;

namespace Pivot.Engine.Data;

public class PivotDbContext : DbContext
{
    public DbSet<AssetEntity> Assets { get; set; }
    public DbSet<AssetColor> AssetColors { get; set; }
    public DbSet<SmartFolder> SmartFolders { get; set; }

    public PivotDbContext(DbContextOptions<PivotDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global query filter: 論理削除されたものは除外
        modelBuilder.Entity<AssetEntity>()
            .HasQueryFilter(a => !a.IsDeleted);
    }
}

