namespace KeyPulse.Core;

public enum AppPage
{
    Dashboard,
    Keyboard,
    Mouse,
    Trends,
    Reports,
    Apps,
    Settings
}

public sealed record NavigationItem(AppPage Page, string Title)
{
    public string AutomationId => "Nav" + Page;
}

public static class NavigationCatalog
{
    public static IReadOnlyList<NavigationItem> Items { get; } =
    [
        new(AppPage.Dashboard, "总览"),
        new(AppPage.Keyboard, "键盘"),
        new(AppPage.Mouse, "鼠标"),
        new(AppPage.Trends, "趋势"),
        new(AppPage.Reports, "报告"),
        new(AppPage.Apps, "应用"),
        new(AppPage.Settings, "设置")
    ];
}
