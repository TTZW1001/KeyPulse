using KeyPulse.Core;
using KeyPulse.Infrastructure.System;
using Xunit;

namespace KeyPulse.Tests;

public class LifecycleTests
{
    [Fact]
    public void StartupFlag_IsSilent()
    {
        Assert.True(LaunchArguments.IsSilentStartup(new[] { "--startup" }));
        Assert.True(LaunchArguments.IsSilentStartup(new[] { "KeyPulse.exe", "--startup" }));
        Assert.False(LaunchArguments.IsSilentStartup(Array.Empty<string>()));
        Assert.False(LaunchArguments.IsSilentStartup(new[] { "--other" }));
    }

    [Fact]
    public void StartupService_WritesQuotedCommandLine_WithoutTouchingHkcu()
    {
        var store = new FakeRunKeyStore();
        var service = new StartupService(store, @"C:\Apps\KeyPulse.exe", "KeyPulse.Test");

        Assert.Equal("\"C:\\Apps\\KeyPulse.exe\" --startup", service.CommandLine);
        Assert.False(service.IsEnabled);

        service.Enable();
        Assert.True(service.IsEnabled);
        Assert.Equal(service.CommandLine, store.GetValue("KeyPulse.Test"));

        service.Disable();
        Assert.False(service.IsEnabled);
        Assert.Null(store.GetValue("KeyPulse.Test"));
    }

    [Fact]
    public void SingleInstance_SecondAcquireIsNotPrimary()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var mutexName = @"Local\KeyPulse.Test.Mutex." + suffix;
        var eventName = @"Local\KeyPulse.Test.Show." + suffix;

        using var first = new SingleInstanceService(mutexName, eventName);
        using var second = new SingleInstanceService(mutexName, eventName);

        Assert.True(first.TryAcquire());
        Assert.True(first.IsPrimary);
        Assert.False(second.TryAcquire());
        Assert.False(second.IsPrimary);
    }

    [Fact]
    public void SingleInstance_ReleasedMutexCanBeAcquiredAgain()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var mutexName = @"Local\KeyPulse.Test.Mutex." + suffix;
        var eventName = @"Local\KeyPulse.Test.Show." + suffix;

        using (var first = new SingleInstanceService(mutexName, eventName))
        {
            Assert.True(first.TryAcquire());
        }

        using var again = new SingleInstanceService(mutexName, eventName);
        Assert.True(again.TryAcquire());
    }

    private sealed class FakeRunKeyStore : IRunKeyStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

        public string? GetValue(string name) =>
            _values.TryGetValue(name, out var value) ? value : null;

        public void SetValue(string name, string value) => _values[name] = value;

        public void DeleteValue(string name) => _values.Remove(name);
    }
}
