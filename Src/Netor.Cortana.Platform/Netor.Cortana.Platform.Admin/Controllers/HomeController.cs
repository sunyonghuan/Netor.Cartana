using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Netor.Cortana.Platform.Admin.Models;
using Netor.Cortana.Platform.Admin.Models.Dashboard;
using Netor.Cortana.Platform.Entitys.Data;

namespace Netor.Cortana.Platform.Admin.Controllers;

public class HomeController(PlatformDbContext dbContext) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new DashboardViewModel
        {
            Metrics =
            [
                new DashboardMetric("资源总数", (await dbContext.Assets.CountAsync(cancellationToken)).ToString(), "插件、技能、智能体、解决方案", "▣", "#1e6fff"),
                new DashboardMetric("个人用户", (await dbContext.Accounts.CountAsync(cancellationToken)).ToString(), "当前平台注册账户", "☷", "#16a34a"),
                new DashboardMetric("订阅记录", (await dbContext.Subscriptions.CountAsync(cancellationToken)).ToString(), "免费和付费订阅", "◈", "#7c3aed"),
                new DashboardMetric("销售单", (await dbContext.Orders.CountAsync(cancellationToken)).ToString(), "资源订阅和购买订单", "￥", "#ea580c")
            ],
            QuickLinks =
            [
                new DashboardQuickLink("资源管理", "维护插件、技能、智能体和解决方案。", "Assets", "Index", "▣"),
                new DashboardQuickLink("分类管理", "维护市场导航和资源分类。", "Home", "Index", "◎"),
                new DashboardQuickLink("订阅管理", "查看用户订阅状态和有效期。", "Home", "Index", "◈"),
                new DashboardQuickLink("订单交易", "查看销售单、支付状态和交易记录。", "Home", "Index", "￥"),
                new DashboardQuickLink("用户账户", "管理个人用户、角色和钱包。", "Home", "Index", "☷"),
                new DashboardQuickLink("系统设置", "维护平台名称、文件存储和下载策略。", "Home", "Index", "⚙")
            ]
        };

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
