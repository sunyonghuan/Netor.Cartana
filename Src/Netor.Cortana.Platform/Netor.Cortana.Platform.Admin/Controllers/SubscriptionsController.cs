using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Netor.Cortana.Platform.Admin.Models.Subscriptions;
using Netor.Cortana.Platform.Entitys.Data;
using Netor.Cortana.Platform.Entitys.Enums;

namespace Netor.Cortana.Platform.Admin.Controllers;

public sealed class SubscriptionsController(PlatformDbContext dbContext) : Controller
{
    public async Task<IActionResult> Index(
        string? keyword,
        SubscriptionStatus? status,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 10;
        page = Math.Max(1, page);

        var query = dbContext.Subscriptions
            .AsNoTracking()
            .Include(x => x.Account)
            .Include(x => x.Asset)
            .Include(x => x.PricingPlan)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim();
            query = query.Where(x =>
                (x.Account != null && x.Account.LoginUserName.Contains(normalizedKeyword)) ||
                (x.Asset != null && x.Asset.Name.Contains(normalizedKeyword)) ||
                (x.PricingPlan != null && x.PricingPlan.Name.Contains(normalizedKeyword)));
        }

        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);

        var items = await query
            .OrderByDescending(x => x.ID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SubscriptionListItem(
                x.ID,
                x.Account == null ? "未知用户" : x.Account.LoginUserName,
                x.Asset == null ? "未知资源" : x.Asset.Name,
                x.PricingPlan == null ? "未知方案" : x.PricingPlan.Name,
                GetSubscriptionStatusName(x.Status),
                x.Status,
                x.StartedAtUtc,
                x.ExpiresAtUtc,
                x.CanceledAtUtc,
                x.OrderId,
                dbContext.DownloadRecords.Count(d => d.SubscriptionId == x.ID)))
            .ToListAsync(cancellationToken);

        var model = new SubscriptionIndexViewModel
        {
            Items = items,
            Keyword = keyword,
            Status = status,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalCount = totalCount,
            ActiveCount = await dbContext.Subscriptions.CountAsync(x => x.Status == SubscriptionStatus.Active, cancellationToken),
            ExpiredCount = await dbContext.Subscriptions.CountAsync(x => x.Status == SubscriptionStatus.Expired, cancellationToken),
            CanceledCount = await dbContext.Subscriptions.CountAsync(x => x.Status == SubscriptionStatus.Canceled, cancellationToken),
            DownloadCount = await dbContext.DownloadRecords.CountAsync(cancellationToken)
        };

        return View(model);
    }

    public async Task<IActionResult> Downloads(string? keyword, int page = 1, CancellationToken cancellationToken = default)
    {
        const int pageSize = 10;
        page = Math.Max(1, page);

        var query = dbContext.DownloadRecords
            .AsNoTracking()
            .Include(x => x.Account)
            .Include(x => x.Asset)
            .Include(x => x.AssetVersion)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim();
            query = query.Where(x =>
                (x.Account != null && x.Account.LoginUserName.Contains(normalizedKeyword)) ||
                (x.Asset != null && x.Asset.Name.Contains(normalizedKeyword)) ||
                (x.AssetVersion != null && x.AssetVersion.VersionName.Contains(normalizedKeyword)) ||
                (x.IpAddress != null && x.IpAddress.Contains(normalizedKeyword)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);

        var items = await query
            .OrderByDescending(x => x.ID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new DownloadRecordListItem(
                x.Account == null ? "未知用户" : x.Account.LoginUserName,
                x.Asset == null ? "未知资源" : x.Asset.Name,
                x.AssetVersion == null ? "未知版本" : x.AssetVersion.VersionName,
                x.IpAddress,
                x.UserAgent,
                x.TimeStamp))
            .ToListAsync(cancellationToken);

        var model = new DownloadRecordIndexViewModel
        {
            Items = items,
            Keyword = keyword,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalCount = totalCount
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string id, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.Subscriptions.FirstOrDefaultAsync(x => x.ID == id, cancellationToken);
        if (subscription is null)
        {
            return NotFound();
        }

        subscription.Status = SubscriptionStatus.Canceled;
        subscription.CanceledAtUtc ??= DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Batch(string[] ids, string operation, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            TempData["AdminToast"] = "请先选择要操作的订阅。";
            return RedirectToAction(nameof(Index));
        }

        var subscriptions = await dbContext.Subscriptions
            .Where(x => ids.Contains(x.ID))
            .ToListAsync(cancellationToken);

        foreach (var subscription in subscriptions)
        {
            switch (operation)
            {
                case "active":
                    subscription.Status = subscription.ExpiresAtUtc <= DateTimeOffset.UtcNow ? SubscriptionStatus.Expired : SubscriptionStatus.Active;
                    subscription.CanceledAtUtc = null;
                    break;
                case "cancel":
                    subscription.Status = SubscriptionStatus.Canceled;
                    subscription.CanceledAtUtc ??= DateTimeOffset.UtcNow;
                    break;
                case "expired":
                    subscription.Status = SubscriptionStatus.Expired;
                    break;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminToast"] = $"已批量处理 {subscriptions.Count} 条订阅。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(string id, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.Subscriptions.FirstOrDefaultAsync(x => x.ID == id, cancellationToken);
        if (subscription is null)
        {
            return NotFound();
        }

        subscription.Status = subscription.ExpiresAtUtc <= DateTimeOffset.UtcNow ? SubscriptionStatus.Expired : SubscriptionStatus.Active;
        subscription.CanceledAtUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private static string GetSubscriptionStatusName(SubscriptionStatus status) => status switch
    {
        SubscriptionStatus.Active => "有效",
        SubscriptionStatus.Expired => "已过期",
        SubscriptionStatus.Canceled => "已取消",
        _ => status.ToString()
    };
}
