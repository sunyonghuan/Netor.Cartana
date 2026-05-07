using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Netor.Cortana.Platform.Admin.Models.Accounts;
using Netor.Cortana.Platform.Entitys.Data;
using Netor.Cortana.Platform.Entitys.Tables.Accounts;
using Netor.Extensions.EncryptExtensions;

namespace Netor.Cortana.Platform.Admin.Controllers;

public sealed class AccountsController(PlatformDbContext dbContext) : Controller
{
    public async Task<IActionResult> Index(string? keyword, byte? status, int page = 1, CancellationToken cancellationToken = default)
    {
        const int pageSize = 10;
        page = Math.Max(1, page);

        var query = dbContext.Accounts
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim();
            query = query.Where(x =>
                x.LoginUserName.Contains(normalizedKeyword) ||
                x.Phone.Contains(normalizedKeyword) ||
                x.Email.Contains(normalizedKeyword) ||
                x.NickName.Contains(normalizedKeyword) ||
                x.RealName.Contains(normalizedKeyword));
        }

        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);

        var accounts = await query
            .OrderByDescending(x => x.ID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = accounts.Select(x => new AccountListItem(
                x.ID,
                x.No,
                x.LoginUserName,
                string.IsNullOrWhiteSpace(x.NickName) ? "-" : x.NickName,
                x.Phone,
                x.Email,
                x.Status,
                x.AccountType,
                x.LoginTimes,
                x.LastLoginTime,
                dbContext.AccountWallets.Where(w => EF.Property<string>(w, "AccountID") == x.ID).Sum(w => w.Money),
                dbContext.Subscriptions.Count(s => s.AccountId == x.ID),
                dbContext.Orders.Count(o => EF.Property<string>(o, "AccountID") == x.ID)))
            .ToList();

        var enabledStatus = (byte)0;
        var model = new AccountIndexViewModel
        {
            Items = items,
            Keyword = keyword,
            Status = status,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalCount = totalCount,
            EnabledCount = await dbContext.Accounts.CountAsync(x => x.Status == enabledStatus, cancellationToken),
            DisabledCount = await dbContext.Accounts.CountAsync(x => x.Status != enabledStatus, cancellationToken),
            WalletTotal = await dbContext.AccountWallets.SumAsync(x => x.Money, cancellationToken),
            SubscriptionCount = await dbContext.Subscriptions.CountAsync(cancellationToken)
        };

        return View(model);
    }

    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ID == id, cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        var wallets = await dbContext.AccountWallets
            .AsNoTracking()
            .Where(x => EF.Property<string>(x, "AccountID") == account.ID)
            .Select(x => new AccountWalletListItem(x.Name, x.Money, x.Type, x.Status))
            .ToListAsync(cancellationToken);

        var roles = await dbContext.AccountRolePairs
            .AsNoTracking()
            .Include(x => x.Role)
            .Where(x => EF.Property<string>(x, "AccountID") == account.ID && x.Role != null)
            .Select(x => new AccountRoleListItem(x.Role!.Name, x.Role.Power, x.Role.Enabel))
            .ToListAsync(cancellationToken);

        var properties = await dbContext.AccountPropertys
            .AsNoTracking()
            .Where(x => EF.Property<string>(x, "AccountID") == account.ID)
            .Select(x => new AccountPropertyListItem(x.Key, x.Value, x.Display, x.Group))
            .ToListAsync(cancellationToken);

        var model = new AccountDetailViewModel
        {
            Id = account.ID,
            No = account.No,
            LoginUserName = account.LoginUserName,
            NickName = string.IsNullOrWhiteSpace(account.NickName) ? "-" : account.NickName,
            RealName = string.IsNullOrWhiteSpace(account.RealName) ? "-" : account.RealName,
            Phone = account.Phone,
            Email = account.Email,
            PhoneConfirmed = account.PhoneConfirmed,
            EmailConfirmed = account.EmailConfirmed,
            Status = account.Status,
            AccountType = account.AccountType,
            LoginTimes = account.LoginTimes,
            LastLoginTime = account.LastLoginTime,
            LoginIP = string.IsNullOrWhiteSpace(account.LoginIP) ? "-" : account.LoginIP,
            TimeStamp = account.TimeStamp,
            Wallets = wallets,
            Roles = roles,
            Properties = properties,
            SubscriptionCount = await dbContext.Subscriptions.CountAsync(x => x.AccountId == account.ID, cancellationToken),
            OrderCount = await dbContext.Orders.CountAsync(x => EF.Property<string>(x, "AccountID") == account.ID, cancellationToken),
            DownloadCount = await dbContext.DownloadRecords.CountAsync(x => x.AccountId == account.ID, cancellationToken)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchStatus(string[] ids, byte status, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            TempData["AdminToast"] = "请先选择要操作的用户。";
            return RedirectToAction(nameof(Index));
        }

        var accounts = await dbContext.Accounts
            .Where(x => ids.Contains(x.ID))
            .ToListAsync(cancellationToken);

        foreach (var account in accounts)
        {
            account.Status = status;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminToast"] = $"已批量更新 {accounts.Count} 个用户状态。";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.ID == id, cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        return View(new AccountEditViewModel
        {
            Id = account.ID,
            LoginUserName = account.LoginUserName,
            NickName = account.NickName,
            RealName = account.RealName,
            Phone = account.Phone,
            Email = account.Email,
            PhoneConfirmed = account.PhoneConfirmed,
            EmailConfirmed = account.EmailConfirmed
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AccountEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var account = await dbContext.Accounts.FirstOrDefaultAsync(x => x.ID == model.Id, cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        account.NickName = model.NickName.Trim();
        account.RealName = model.RealName.Trim();
        account.Phone = model.Phone.Trim();
        account.Email = model.Email.Trim();
        account.PhoneConfirmed = model.PhoneConfirmed;
        account.EmailConfirmed = model.EmailConfirmed;
        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["AdminToast"] = "用户基本信息已保存。";
        return RedirectToAction(nameof(Details), new { id = account.ID });
    }

    public async Task<IActionResult> Status(string id, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.ID == id, cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        return View(new AccountStatusViewModel
        {
            Id = account.ID,
            LoginUserName = account.LoginUserName,
            Status = account.Status
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Status(AccountStatusViewModel model, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(x => x.ID == model.Id, cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        account.Status = model.Status;
        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["AdminToast"] = "用户状态已更新。";
        return RedirectToAction(nameof(Details), new { id = account.ID });
    }

    public async Task<IActionResult> Recharge(string id, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.ID == id, cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        return View(new AccountRechargeViewModel
        {
            Id = account.ID,
            LoginUserName = account.LoginUserName,
            CurrentBalance = await GetWalletBalanceAsync(account.ID, cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Recharge(AccountRechargeViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.CurrentBalance = await GetWalletBalanceAsync(model.Id, cancellationToken);
            return View(model);
        }

        var account = await dbContext.Accounts.FirstOrDefaultAsync(x => x.ID == model.Id, cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        var wallet = await dbContext.AccountWallets.FirstOrDefaultAsync(x => EF.Property<string>(x, "AccountID") == account.ID, cancellationToken);
        if (wallet is null)
        {
            wallet = dbContext.AccountWallets.Add(new AccountWallet
            {
                Account = account,
                Name = "余额钱包",
                Type = 1,
                Status = 0
            }).Entity;
        }

        wallet.Money += model.Amount;
        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["AdminToast"] = $"已为用户充值 {model.Amount:0.00}。";
        return RedirectToAction(nameof(Details), new { id = account.ID });
    }

    public async Task<IActionResult> Password(string id, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.ID == id, cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        return View(new AccountPasswordViewModel
        {
            Id = account.ID,
            LoginUserName = account.LoginUserName
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Password(AccountPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var account = await dbContext.Accounts.FirstOrDefaultAsync(x => x.ID == model.Id, cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        account.LoginPassword = model.NewPassword.MD5Encrypt();
        if (!string.IsNullOrWhiteSpace(model.NewSafePassword))
        {
            account.SafePassword = model.NewSafePassword.MD5Encrypt();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["AdminToast"] = "用户密码已重置。";
        return RedirectToAction(nameof(Details), new { id = account.ID });
    }

    private async Task<decimal> GetWalletBalanceAsync(string accountId, CancellationToken cancellationToken)
    {
        return await dbContext.AccountWallets
            .Where(x => EF.Property<string>(x, "AccountID") == accountId)
            .SumAsync(x => x.Money, cancellationToken);
    }
}
