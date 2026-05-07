namespace Netor.Cortana.Platform.Admin.Models.Dashboard;

public sealed record DashboardMetric(string Title, string Value, string Description, string Icon, string Color);

public sealed record DashboardQuickLink(string Title, string Description, string Controller, string Action, string Icon);

public sealed class DashboardViewModel
{
    public IReadOnlyList<DashboardMetric> Metrics { get; init; } = [];

    public IReadOnlyList<DashboardQuickLink> QuickLinks { get; init; } = [];
}
