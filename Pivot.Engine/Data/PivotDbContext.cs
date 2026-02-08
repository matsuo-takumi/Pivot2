using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Pivot.Engine.Data;

public class PivotDbContext : DbContext
{
    public DbSet<AssetMetadata> AssetMetadata { get; set; }

    public PivotDbContext(DbContextOptions<PivotDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssetMetadata>()
            .HasKey(a => a.Id);
            
        modelBuilder.Entity<AssetMetadata>()
            .HasIndex(a => a.FilePath)
            .IsUnique();
    }
}

public class AssetMetadata
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string FilePath { get; set; } = string.Empty;

    public string? ThumbnailPath { get; set; }

    public string? ComputeHash { get; set; }

    public long FileSizeBytes { get; set; }

    public long LastModifiedTicks { get; set; }
    
    public string? Tags { get; set; } // JSON or comma-separated
}
