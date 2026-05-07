namespace Netor.Cortana.Platform.Admin.Models.Assets;

using Netor.Cortana.Platform.Entitys.Enums;

public sealed record AssetListItem(
    string Id,
    string Name,
    string Slug,
    string DeveloperName,
    string ShortDescription,
    AssetType Type,
    string TypeName,
    AssetStatus Status,
    string StatusName,
    string? CategoryName,
    bool IsFeatured,
    int DownloadCount,
    int VersionCount,
    int PricingPlanCount,
    DateTimeOffset? PublishedAtUtc);

public sealed record AssetCategoryOption(string Id, string Name);

public sealed record AssetVersionItem(string VersionName, string ReleaseNotes, long PackageSize, string FilePath);

public sealed record AssetPricingPlanItem(string Name, string PlanTypeName, decimal Price, string Currency, int DurationDays, bool IsActive);

public sealed class AssetDetailViewModel
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string DeveloperName { get; init; } = string.Empty;

    public string ShortDescription { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Tags { get; init; } = string.Empty;

    public string TypeName { get; init; } = string.Empty;

    public string StatusName { get; init; } = string.Empty;

    public string? CategoryName { get; init; }

    public bool IsFeatured { get; init; }

    public int DownloadCount { get; init; }

    public DateTimeOffset? PublishedAtUtc { get; init; }

    public IReadOnlyList<AssetVersionItem> Versions { get; init; } = [];

    public IReadOnlyList<AssetPricingPlanItem> PricingPlans { get; init; } = [];
}

public sealed class AssetIndexViewModel
{
    public IReadOnlyList<AssetListItem> Items { get; init; } = [];

    public IReadOnlyList<AssetCategoryOption> Categories { get; init; } = [];

    public string? Keyword { get; init; }

    public AssetType? Type { get; init; }

    public AssetStatus? Status { get; init; }

    public string? CategoryId { get; init; }

    public bool? Featured { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public int TotalPages { get; init; }

    public int TotalCount { get; init; }

    public int PublishedCount { get; init; }

    public int FeaturedCount { get; init; }

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;
}
