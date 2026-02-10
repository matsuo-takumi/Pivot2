using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pivot.Engine.Data;

namespace MigrationHelper
{
    public class PivotDbContextFactory : IDesignTimeDbContextFactory<PivotDbContext>
    {
        public PivotDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PivotDbContext>();
            optionsBuilder.UseSqlite("Data Source=pivot.db");

            return new PivotDbContext(optionsBuilder.Options);
        }
    }
}
