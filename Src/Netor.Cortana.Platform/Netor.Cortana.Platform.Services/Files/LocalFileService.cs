using Microsoft.Extensions.Options;
using Netor.Cortana.Platform.Core.Options;

namespace Netor.Cortana.Platform.Services.Files;

public sealed class LocalFileService(IOptions<FileStorageOptions> options)
{
    private readonly FileStorageOptions _options = options.Value;

    public string GetPackageRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _options.RootPath, "packages"));
}