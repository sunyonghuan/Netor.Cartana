using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Netor.Cortana.Platform.Admin.Models.Assets;
using Netor.Cortana.Platform.Entitys.Data;
using Netor.Cortana.Platform.Entitys.Enums;

namespace Netor.Cortana.Platform.Admin.Controllers;

public sealed class AssetsController(PlatformDbContext dbContext) : Controller
{
    public async Task<IActionResult> Index(
        string? keyword,
        AssetType? type,
        AssetStatus? status,
        string? categoryId,
        bool? featured,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 10;
        page = Math.Max(1, page);

        var query = dbContext.Assets
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Versions)
            .Include(x => x.PricingPlans)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim();
            query = query.Where(x => x.Name.Contains(normalizedKeyword) || x.Slug.Contains(normalizedKeyword) || x.DeveloperName.Contains(normalizedKeyword));
        }

        if (type is not null)
        {
            query = query.Where(x => x.Type == type);
        }

        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            query = query.Where(x => x.CategoryId == categoryId);
        }

        if (featured is not null)
        {
            query = query.Where(x => x.IsFeatured == featured);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);

        var items = await query
            .OrderByDescending(x => x.TimeStamp)
            .ThenBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AssetListItem(
                x.ID,
                x.Name,
                x.Slug,
                x.DeveloperName,
                x.ShortDescription,
                x.Type,
                GetAssetTypeName(x.Type),
                x.Status,
                GetAssetStatusName(x.Status),
                x.Category == null ? null : x.Category.Name,
                x.IsFeatured,
                x.DownloadCount,
                x.Versions.Count,
                x.PricingPlans.Count,
                x.PublishedAtUtc))
            .ToListAsync(cancellationToken);

        var categories = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new AssetCategoryOption(x.ID, x.Name))
            .ToListAsync(cancellationToken);

        var model = new AssetIndexViewModel
        {
            Items = items,
            Categories = categories,
            Keyword = keyword,
            Type = type,
            Status = status,
            CategoryId = categoryId,
            Featured = featured,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalCount = totalCount,
            PublishedCount = await dbContext.Assets.CountAsync(x => x.Status == AssetStatus.Published, cancellationToken),
            FeaturedCount = await dbContext.Assets.CountAsync(x => x.IsFeatured, cancellationToken)
        };

        return View(model);
    }

    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var asset = await dbContext.Assets
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Versions)
            .Include(x => x.PricingPlans)
            .FirstOrDefaultAsync(x => x.ID == id, cancellationToken);

        if (asset is null)
        {
            return NotFound();
        }

        var model = new AssetDetailViewModel
        {
            Id = asset.ID,
            Name = asset.Name,
            Slug = asset.Slug,
            DeveloperName = asset.DeveloperName,
            ShortDescription = asset.ShortDescription,
            Description = asset.Description,
            Tags = asset.Tags,
            TypeName = GetAssetTypeName(asset.Type),
            StatusName = GetAssetStatusName(asset.Status),
            CategoryName = asset.Category?.Name,
            IsFeatured = asset.IsFeatured,
            DownloadCount = asset.DownloadCount,
            PublishedAtUtc = asset.PublishedAtUtc,
            Versions = asset.Versions
                .OrderByDescending(x => x.TimeStamp)
                .Select(x => new AssetVersionItem(x.VersionName, x.ReleaseNotes, x.PackageSize, x.FilePath))
                .ToList(),
            PricingPlans = asset.PricingPlans
                .OrderBy(x => x.Price)
                .Select(x => new AssetPricingPlanItem(x.Name, GetPricingPlanTypeName(x.PlanType), x.Price, x.Currency, x.DurationDays, x.IsActive))
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Batch(string[] ids, string operation, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            TempData["AdminToast"] = "请先选择要操作的资源。";
            return RedirectToAction(nameof(Index));
        }

        var assets = await dbContext.Assets
            .Where(x => ids.Contains(x.ID))
            .ToListAsync(cancellationToken);

        foreach (var asset in assets)
        {
            switch (operation)
            {
                case "publish":
                    asset.Status = AssetStatus.Published;
                    asset.PublishedAtUtc ??= DateTimeOffset.UtcNow;
                    break;
                case "offline":
                    asset.Status = AssetStatus.Offline;
                    break;
                case "hidden":
                    asset.Status = AssetStatus.Hidden;
                    break;
                case "draft":
                    asset.Status = AssetStatus.Draft;
                    break;
                case "feature":
                    asset.IsFeatured = true;
                    break;
                case "unfeature":
                    asset.IsFeatured = false;
                    break;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminToast"] = $"已批量处理 {assets.Count} 个资源。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFeatured(string id, CancellationToken cancellationToken)
    {
        var asset = await dbContext.Assets.FirstOrDefaultAsync(x => x.ID == id, cancellationToken);
        if (asset is null)
        {
            return NotFound();
        }

        asset.IsFeatured = !asset.IsFeatured;
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(string id, CancellationToken cancellationToken)
    {
        var asset = await dbContext.Assets.FirstOrDefaultAsync(x => x.ID == id, cancellationToken);
        if (asset is null)
        {
            return NotFound();
        }

        asset.Status = AssetStatus.Published;
        asset.PublishedAtUtc ??= DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Offline(string id, CancellationToken cancellationToken)
    {
        var asset = await dbContext.Assets.FirstOrDefaultAsync(x => x.ID == id, cancellationToken);
        if (asset is null)
        {
            return NotFound();
        }

        asset.Status = AssetStatus.Offline;
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private static string GetAssetTypeName(AssetType type) => type switch
    {
        AssetType.Plugin => "插件",
        AssetType.Skill => "技能",
        AssetType.Agent => "智能体",
        AssetType.Solution => "解决方案",
        _ => type.ToString()
    };

    private static string GetAssetStatusName(AssetStatus status) => status switch
    {
        AssetStatus.Draft => "草稿",
        AssetStatus.Published => "已发布",
        AssetStatus.Hidden => "隐藏",
        AssetStatus.Offline => "下架",
        _ => status.ToString()
    };

    private static string GetPricingPlanTypeName(PricingPlanType type) => type switch
    {
        PricingPlanType.Free => "免费",
        PricingPlanType.Monthly => "月度订阅",
        PricingPlanType.Yearly => "年度订阅",
        _ => type.ToString()
    };
}
