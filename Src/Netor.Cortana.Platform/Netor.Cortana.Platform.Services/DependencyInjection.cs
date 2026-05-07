using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Netor.Cortana.Platform.Core.Options;
using Netor.Cortana.Platform.Services.Files;
using Netor.Cortana.Platform.Services.Market;

namespace Netor.Cortana.Platform.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformServices(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(FileStorageOptions.SectionName);

        services.Configure<FileStorageOptions>(options =>
        {
            options.RootPath = section[nameof(FileStorageOptions.RootPath)] ?? "Data";
        });

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<MarketService>();
        services.AddSingleton<LocalFileService>();

        return services;
    }
}