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
}
