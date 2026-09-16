using KeyPulse.Core;
using Xunit;
using KeyPulse.Core.Interfaces;
using KeyPulse.Infrastructure;
using KeyPulse.Infrastructure.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KeyPulse.Tests;

public class ProductInfoTests
{
    [Fact]
    public void Name_IsKeyPulse()
    {
        Assert.Equal("KeyPulse", ProductInfo.Name);
    }

    [Fact]
    public void Version_IsCurrentRelease()
    {
        Assert.Equal("1.1.4", ProductInfo.Version);
    }

    [Fact]
    public void PrivacyNotice_DoesNotPromiseToRecordText()
    {
        Assert.Contains("不记录输入内容", ProductInfo.PrivacyNotice, StringComparison.Ordinal);
        Assert.DoesNotContain("记录你输入的文字", ProductInfo.PrivacyNotice, StringComparison.Ordinal);
    }
}

public class InfrastructureSmokeTests
{
    [Fact]
    public void AppPaths_UsesLocalAppData()
    {
        var paths = new AppPaths();
        var expectedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KeyPulse");

        Assert.Equal(expectedRoot, paths.RootDirectory);
        Assert.Equal(Path.Combine(expectedRoot, "logs"), paths.LogsDirectory);
        Assert.Equal(Path.Combine(expectedRoot, "data"), paths.DataDirectory);
        Assert.Equal(Path.Combine(expectedRoot, "data", "keypulse.db"), paths.DatabasePath);
        Assert.Equal(Path.Combine(expectedRoot, "config"), paths.ConfigDirectory);
        Assert.Equal(Path.Combine(expectedRoot, "config", "settings.json"), paths.SettingsPath);
    }

    [Fact]
    public async Task ServiceProvider_CanResolveHostAndPaths()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyPulseInfrastructure();

        using var provider = services.BuildServiceProvider();
        var host = provider.GetRequiredService<IAppHost>();
        var paths = provider.GetRequiredService<IAppPaths>();

        Assert.NotNull(host);
        Assert.False(string.IsNullOrWhiteSpace(paths.LogsDirectory));

        await host.StartAsync();
        await host.StopAsync();
    }
}
