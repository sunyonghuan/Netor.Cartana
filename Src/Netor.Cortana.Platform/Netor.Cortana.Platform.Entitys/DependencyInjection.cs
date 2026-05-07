using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Netor.Cortana.Platform.Core.Options;
using Netor.Cortana.Platform.Entitys.Data;

namespace Netor.Cortana.Platform.Entitys;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(DatabaseOptions.SectionName);
        var options = new DatabaseOptions
        {
            Provider = section[nameof(DatabaseOptions.Provider)] ?? "Sqlite",
            ConnectionString = section[nameof(DatabaseOptions.ConnectionString)] ?? "Data Source=Data/platform.db"
        };

        services.AddDbContext<PlatformDbContext>(builder =>
        {
            builder.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));

            if (string.Equals(options.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                builder.UseSqlite(options.ConnectionString);
                return;
            }

            throw new NotSupportedException($"Database provider '{options.Provider}' is not supported yet.");
        });

        return services;
    }
}