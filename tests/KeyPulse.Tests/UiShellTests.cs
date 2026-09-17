using KeyPulse.Core;
using KeyPulse.Infrastructure.System;
using Xunit;

namespace KeyPulse.Tests;

public class UiShellTests
{
    [Fact]
    public void NavigationCatalog_HasSixPagesInOrder()
    {
        Assert.Equal(6, NavigationCatalog.Items.Count);
        Assert.Equal(
            new[]
            {
                AppPage.Dashboard,
                AppPage.Keyboard,
                AppPage.Mouse,
                AppPage.Trends,
                AppPage.Apps,
                AppPage.Settings
            },
            NavigationCatalog.Items.Select(item => item.Page).ToArray());
        Assert.Equal(
            new[] { "总览", "键盘", "鼠标", "趋势", "应用", "设置" },
            NavigationCatalog.Items.Select(item => item.Title).ToArray());
    }

    [Fact]
    public void ThemeMode_HasLightDarkSystem()
    {
        Assert.Equal(
            new[] { ThemeMode.Light, ThemeMode.Dark, ThemeMode.System },
            Enum.GetValues<ThemeMode>());
    }

    [Fact]
    public void UserSettings_Theme_RoundTripsThroughJson()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyPulseTests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);
            var settings = new JsonUserSettings(paths);
            Assert.Equal(ThemeMode.System, settings.Theme);

            settings.Theme = ThemeMode.Dark;
            settings.Save();

            var json = File.ReadAllText(paths.SettingsPath);
            Assert.Contains("\"theme\": \"Dark\"", json, StringComparison.Ordinal);

            var loaded = new JsonUserSettings(paths);
            Assert.Equal(ThemeMode.Dark, loaded.Theme);

            loaded.Theme = ThemeMode.Light;
            loaded.Save();
            Assert.Equal(ThemeMode.Light, new JsonUserSettings(paths).Theme);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void UserSettings_MissingTheme_DefaultsToSystem()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyPulseTests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);
            Directory.CreateDirectory(paths.ConfigDirectory);
            File.WriteAllText(paths.SettingsPath, """{"hideToTrayHintDismissed": true}""");

            var settings = new JsonUserSettings(paths);
            Assert.True(settings.HideToTrayHintDismissed);
            Assert.Equal(ThemeMode.System, settings.Theme);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void UserSettings_ViewPreferences_DefaultAndRoundTripIndependently()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyPulseTests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);
            var settings = new JsonUserSettings(paths);
            Assert.Equal(KeyboardRange.Last7Days, settings.KeyboardRange);
            Assert.Equal(KeyboardRange.Last7Days, settings.MouseRange);
            Assert.Equal(KeyboardRange.Last7Days, settings.MouseHeatmapRange);
            Assert.Equal(KeyboardRange.Last7Days, settings.AppsRange);
            Assert.Equal(TrendRangeKind.Last7Days, settings.TrendRange);
            Assert.Null(settings.TrendCustomFromDate);
            Assert.Null(settings.TrendCustomToDate);
            Assert.Equal(DashboardTrendMetric.Keys, settings.DashboardTrendMetric);

            settings.KeyboardRange = KeyboardRange.Today;
            settings.MouseRange = KeyboardRange.Last30Days;
            settings.MouseHeatmapRange = KeyboardRange.All;
            settings.AppsRange = KeyboardRange.Today;
            settings.TrendRange = TrendRangeKind.Custom;
            settings.TrendCustomFromDate = new DateOnly(2026, 9, 10);
            settings.TrendCustomToDate = new DateOnly(2026, 9, 16);
            settings.DashboardTrendMetric = DashboardTrendMetric.Clicks;
            settings.Save();

            var loaded = new JsonUserSettings(paths);
            Assert.Equal(KeyboardRange.Today, loaded.KeyboardRange);
            Assert.Equal(KeyboardRange.Last30Days, loaded.MouseRange);
            Assert.Equal(KeyboardRange.All, loaded.MouseHeatmapRange);
            Assert.Equal(KeyboardRange.Today, loaded.AppsRange);
            Assert.Equal(TrendRangeKind.Custom, loaded.TrendRange);
            Assert.Equal(new DateOnly(2026, 9, 10), loaded.TrendCustomFromDate);
            Assert.Equal(new DateOnly(2026, 9, 16), loaded.TrendCustomToDate);
            Assert.Equal(DashboardTrendMetric.Clicks, loaded.DashboardTrendMetric);

            var json = File.ReadAllText(paths.SettingsPath);
            Assert.Contains("\"keyboardRange\": \"Today\"", json, StringComparison.Ordinal);
            Assert.Contains("\"trendCustomFromDate\": \"2026-09-10\"", json, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void UserSettings_InvalidNewPreferences_FallBackWithoutLosingExistingSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyPulseTests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);
            Directory.CreateDirectory(paths.ConfigDirectory);
            File.WriteAllText(paths.SettingsPath, """
                {
                  "theme": "Dark",
                  "keyboardLayout": "FullSize",
                  "keyboardRange": "SomethingNew",
                  "mouseRange": "Today",
                  "trendRange": "Unsupported",
                  "trendCustomFromDate": "not-a-date",
                  "dashboardTrendMetric": "Unknown"
                }
                """);

            var settings = new JsonUserSettings(paths);
            Assert.Equal(ThemeMode.Dark, settings.Theme);
            Assert.Equal(KeyboardLayoutKind.FullSize, settings.KeyboardLayout);
            Assert.Equal(KeyboardRange.Last7Days, settings.KeyboardRange);
            Assert.Equal(KeyboardRange.Today, settings.MouseRange);
            Assert.Equal(TrendRangeKind.Last7Days, settings.TrendRange);
            Assert.Null(settings.TrendCustomFromDate);
            Assert.Equal(DashboardTrendMetric.Keys, settings.DashboardTrendMetric);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
