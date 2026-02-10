using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pivot.Engine.Data;

public class PivotDbContextFactory : IDesignTimeDbContextFactory<PivotDbContext>
{
    public PivotDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PivotDbContext>();
        optionsBuilder.UseSqlite("Data Source=pivot.db");

        return new PivotDbContext(optionsBuilder.Options);
    }
}
