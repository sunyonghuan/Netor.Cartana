using Microsoft.EntityFrameworkCore;
using Netor.Cortana.Platform.Entitys.Data;
using Netor.Cortana.Platform.Entitys.Enums;

namespace Netor.Cortana.Platform.Services.Market;

public sealed class MarketService(PlatformDbContext dbContext)
{
    public async Task<IReadOnlyList<MarketAssetListItem>> GetPublishedAssetsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Assets
            .AsNoTracking()
            .Where(x => x.Status == AssetStatus.Published)
            .OrderByDescending(x => x.PublishedAtUtc)
            .Select(x => new MarketAssetListItem(x.ID, x.Type.ToString(), x.Name, x.ShortDescription, x.IconUrl, x.CoverUrl))
            .ToListAsync(cancellationToken);
    }
}

public sealed record MarketAssetListItem(string Id, string Type, string Name, string ShortDescription, string? IconUrl, string? CoverUrl);