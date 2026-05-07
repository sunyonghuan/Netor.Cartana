using Microsoft.EntityFrameworkCore.Design;

namespace Netor.Cortana.Platform.Entitys.Data;

public sealed class PlatformDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CORTANA_PLATFORM_CS")
            ?? "Data Source=Data/platform.db";
        var optionsBuilder = new DbContextOptionsBuilder<PlatformDbContext>();
        optionsBuilder.UseSqlite(connectionString);
        return new PlatformDbContext(optionsBuilder.Options);
    }
}