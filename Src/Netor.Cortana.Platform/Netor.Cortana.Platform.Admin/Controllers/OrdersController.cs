using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Netor.Cortana.Platform.Admin.Models.Orders;
using Netor.Cortana.Platform.Entitys.Data;
using Netor.Cortana.Platform.Entitys.Tables.Orders;

namespace Netor.Cortana.Platform.Admin.Controllers;

public sealed class OrdersController(PlatformDbContext dbContext) : Controller
{
    public async Task<IActionResult> Index(string? keyword, byte? status, int page = 1, CancellationToken cancellationToken = default)
    {
        const int pageSize = 10;
        page = Math.Max(1, page);

        var query = dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Account)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim();
            query = query.Where(x =>
                x.No.Contains(normalizedKeyword) ||
                x.Title.Contains(normalizedKeyword) ||
                x.Content.Contains(normalizedKeyword) ||
                (x.Account != null && x.Account.LoginUserName.Contains(normalizedKeyword)));
        }

        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);

        var assetNames = await dbContext.Assets
            .AsNoTracking()
            .ToDictionaryAsync(x => x.ID, x => x.Name, cancellationToken);
        var pricingPlanNames = await dbContext.PricingPlans
            .AsNoTracking()
            .ToDictionaryAsync(x => x.ID, x => x.Name, cancellationToken);

        var orders = await query
            .OrderByDescending(x => x.ID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = orders.Select(x => new OrderListItem(
                x.ID,
                x.No,
                x.Title,
                x.Account == null ? "未知用户" : x.Account.LoginUserName,
                GetLookupName(assetNames, x.AssetId, "未知资源"),
                GetLookupName(pricingPlanNames, x.PricingPlanId, "未知方案"),
                x.Money,
                x.Numbers,
                x.PayStatus,
                x.PayMethod,
                x.Status,
                x.PayTime,
                x.TimeStamp,
                dbContext.Transactions.Count(t => t.OrderId == x.ID || t.OrderNo == x.No)))
            .ToList();

        var paidStatus = (byte)2;
        var pendingStatus = (byte)1;
        var model = new OrderIndexViewModel
        {
            Items = items,
            Keyword = keyword,
            Status = status,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalCount = totalCount,
            PaidCount = await dbContext.Orders.CountAsync(x => x.PayStatus == paidStatus, cancellationToken),
            PendingCount = await dbContext.Orders.CountAsync(x => x.PayStatus == pendingStatus, cancellationToken),
            PaidAmount = await dbContext.Orders.Where(x => x.PayStatus == paidStatus).SumAsync(x => x.Money, cancellationToken),
            TransactionCount = await dbContext.Transactions.CountAsync(cancellationToken)
        };

        return View(model);
    }

    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Account)
            .FirstOrDefaultAsync(x => x.ID == id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var assetName = await dbContext.Assets
            .AsNoTracking()
            .Where(x => x.ID == order.AssetId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "未知资源";
        var pricingPlanName = await dbContext.PricingPlans
            .AsNoTracking()
            .Where(x => x.ID == order.PricingPlanId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "未知方案";

        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .Include(x => x.Account)
            .Where(x => x.OrderId == order.ID || x.OrderNo == order.No)
            .OrderByDescending(x => x.ID)
            .Select(x => new TransactionListItem(
                x.ID,
                x.No,
                x.OrderNo,
                x.ThirdNo,
                x.Account == null ? "未知用户" : x.Account.LoginUserName,
                x.Title,
                x.Money,
                x.RealMoney,
                x.Type,
                x.PayStatus,
                x.PayMethod,
                x.PayTime,
                x.TimeStamp))
            .ToListAsync(cancellationToken);

        var model = new OrderDetailViewModel
        {
            Id = order.ID,
            No = order.No,
            Title = order.Title,
            AccountName = order.Account == null ? "未知用户" : order.Account.LoginUserName,
            AssetName = assetName,
            PricingPlanName = pricingPlanName,
            Content = order.Content,
            Money = order.Money,
            Numbers = order.Numbers,
            PayStatus = order.PayStatus,
            PayMethod = order.PayMethod,
            Status = order.Status,
            PayTime = order.PayTime,
            TimeStamp = order.TimeStamp,
            Transactions = transactions
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchOrders(string[] ids, byte status, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            TempData["AdminToast"] = "请先选择要操作的订单。";
            return RedirectToAction(nameof(Index));
        }

        var orders = await dbContext.Orders
            .Where(x => ids.Contains(x.ID))
            .ToListAsync(cancellationToken);

        foreach (var order in orders)
        {
            order.Status = status;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminToast"] = $"已批量更新 {orders.Count} 条订单。";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Transactions(string? keyword, byte? payStatus, int page = 1, CancellationToken cancellationToken = default)
    {
        const int pageSize = 10;
        page = Math.Max(1, page);

        var query = dbContext.Transactions
            .AsNoTracking()
            .Include(x => x.Account)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim();
            query = query.Where(x =>
                x.No.Contains(normalizedKeyword) ||
                x.OrderNo.Contains(normalizedKeyword) ||
                x.ThirdNo.Contains(normalizedKeyword) ||
                x.Title.Contains(normalizedKeyword) ||
                (x.Account != null && x.Account.LoginUserName.Contains(normalizedKeyword)));
        }

        if (payStatus is not null)
        {
            query = query.Where(x => x.PayStatus == payStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);

        var items = await query
            .OrderByDescending(x => x.ID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TransactionListItem(
                x.ID,
                x.No,
                x.OrderNo,
                x.ThirdNo,
                x.Account == null ? "未知用户" : x.Account.LoginUserName,
                x.Title,
                x.Money,
                x.RealMoney,
                x.Type,
                x.PayStatus,
                x.PayMethod,
                x.PayTime,
                x.TimeStamp))
            .ToListAsync(cancellationToken);

        var model = new TransactionIndexViewModel
        {
            Items = items,
            Keyword = keyword,
            PayStatus = payStatus,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalCount = totalCount
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchTransactions(string[] ids, byte payStatus, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            TempData["AdminToast"] = "请先选择要操作的交易流水。";
            return RedirectToAction(nameof(Transactions));
        }

        var transactions = await dbContext.Transactions
            .Where(x => ids.Contains(x.ID))
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
        {
            transaction.PayStatus = payStatus;
            if (payStatus == 2)
            {
                transaction.PayTime ??= DateTime.Now;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminToast"] = $"已批量更新 {transactions.Count} 条交易流水。";
        return RedirectToAction(nameof(Transactions));
    }

    private static string GetLookupName(IReadOnlyDictionary<string, string> values, string? id, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(id) && values.TryGetValue(id, out var name))
        {
            return name;
        }

        return fallback;
    }
}
