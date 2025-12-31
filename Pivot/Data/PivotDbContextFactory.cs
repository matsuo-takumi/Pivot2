using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pivot.Data
{
    /// <summary>
    /// Design-time factory for PivotDbContext.
    /// This class is ONLY used by "dotnet ef migrations" commands, never at runtime.
    /// It allows EF Core tools to create a DbContext without starting the WinUI application.
    /// </summary>
    public class PivotDbContextFactory : IDesignTimeDbContextFactory<PivotDbContext>
    {
        public PivotDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PivotDbContext>();
            
            // Development path for migration generation
            // The actual production path is configured in App.xaml.cs
            optionsBuilder.UseSqlite("Data Source=pivot_dev.db");

            return new PivotDbContext(optionsBuilder.Options);
        }
    }
}
