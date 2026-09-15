using KeyPulse.Core;
using KeyPulse.Core.Events;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Models;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Aggregation;
using KeyPulse.Infrastructure.Persistence;
using KeyPulse.Infrastructure.Persistence.Repositories;
using KeyPulse.Infrastructure.Query;
using KeyPulse.Infrastructure.System;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KeyPulse.Tests;

public class SettingsTests : IDisposable
{
    private readonly string _root;
    private readonly AppPaths _paths;
    private readonly SqliteConnectionFactory _factory;
    private readonly DatabaseInitializer _initializer;
    private readonly StatisticsRepository _repository;

    public SettingsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "KeyPulseTests", Guid.NewGuid().ToString("N"));
        _paths = new AppPaths(_root);
        _factory = new SqliteConnectionFactory(_paths);
        _initializer = new DatabaseInitializer(_factory, new MigrationRunner());
        _initializer.Initialize();
        _repository = new StatisticsRepository(_factory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // temp cleanup is best-effort
        }
    }

    [Fact]
    public void Exclude_SavesFileNameOnly_NotFullPath()
    {
        var list = new ExcludedAppList(_paths);
        list.Exclude(@"C:\Program Files\KeePass\KeePass.exe");

        Assert.True(list.IsExcluded("KeePass.exe"));
        Assert.True(list.IsExcluded("keepass.EXE"));
        var json = File.ReadAllText(_paths.ExcludedAppsPath);
        Assert.Contains("KeePass.exe", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Program Files", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":\\", json, StringComparison.Ordinal);
        Assert.DoesNotContain("/", json, StringComparison.Ordinal);

        var reloaded = new ExcludedAppList(_paths);
        Assert.Contains(reloaded.Entries(), item => item.ProcessName == "KeePass.exe" && !item.IsDefault);
    }

    [Fact]
    public void Remove_IgnoresDefaultEntries()
    {
        var list = new ExcludedAppList(_paths);
        list.Exclude("KeePass.exe");
        list.Remove("LockApp.exe");
        list.Remove("LogonUI.exe");
        Assert.True(list.IsExcluded("LockApp.exe"));
        Assert.True(list.IsExcluded("LogonUI.exe"));
        Assert.True(list.IsExcluded("KeePass.exe"));

        list.Remove(@"D:\Tools\KeePass.exe");
        Assert.False(list.IsExcluded("KeePass.exe"));
        var json = File.ReadAllText(_paths.ExcludedAppsPath);
        Assert.DoesNotContain("KeePass.exe", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Clear_EmptiesStats_KeepsExcludedJson()
    {
        var list = new ExcludedAppList(_paths);
        list.Exclude("KeePass.exe");
        using var aggregator = CreateAggregator();
        aggregator.Record(Key("A"));
        aggregator.Record(new MouseButtonEvent { Timestamp = DateTimeOffset.Now, Button = MouseButton.Left });
        await _repository.FlushAsync(aggregator.SwapForFlush());
        aggregator.Record(Key("B"));

        var flush = new FlushService(
            aggregator,
            _repository,
            _initializer,
            new PersistenceOptions(),
            NullLogger<FlushService>.Instance);
        await flush.ClearStatisticsAsync();

        var today = DateOnly.FromDateTime(DateTime.Now);
        Assert.Empty(await _repository.GetKeyStatsAsync(today, today));
        Assert.Empty(await _repository.GetMouseStatsAsync(today, today));
        Assert.Empty(await _repository.GetHourlyAsync(today, today));
        Assert.Empty(await _repository.GetAppStatsAsync(today, today));
        Assert.Empty(aggregator.CaptureSnapshot().KeyCounts);
        Assert.Empty(aggregator.CaptureUnflushed().KeyCountsByDate);

        Assert.True(File.Exists(_paths.ExcludedAppsPath));
        var json = File.ReadAllText(_paths.ExcludedAppsPath);
        Assert.Contains("KeePass.exe", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(list.IsExcluded("KeePass.exe"));
    }

    [Fact]
    public async Task ExportCsv_HeadersHaveNoTitleUrlPath()
    {
        using var aggregator = CreateAggregator();
        aggregator.Record(Key("A"));
        aggregator.Record(new MouseButtonEvent { Timestamp = DateTimeOffset.Now, Button = MouseButton.Left });
        await _repository.FlushAsync(aggregator.SwapForFlush());

        var exportDir = Path.Combine(_root, "export");
        var export = new StatisticsExportService(_factory);
        var files = await export.ExportCsvAsync(exportDir);
        Assert.Equal(4, files.Count);

        foreach (var file in files)
        {
            Assert.True(File.Exists(file));
            var name = Path.GetFileName(file);
            Assert.Contains(DateTime.Now.ToString("yyyy-MM-dd"), name, StringComparison.Ordinal);
            var header = File.ReadLines(file).First();
            Assert.DoesNotContain("title", header, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("url", header, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("path", header, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("command", header, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("clipboard", header, StringComparison.OrdinalIgnoreCase);
        }

        var appHeader = File.ReadLines(files.Single(path => path.Contains("daily-app-stats", StringComparison.Ordinal))).First();
        Assert.Contains("process_name", appHeader, StringComparison.Ordinal);
        Assert.Contains("display_name", appHeader, StringComparison.Ordinal);
        Assert.Contains("key_press_count", appHeader, StringComparison.Ordinal);

        var keyHeader = File.ReadLines(files.Single(path => path.Contains("daily-key-stats", StringComparison.Ordinal))).First();
        Assert.Equal("stat_date,key_code,press_count", keyHeader);

        var bom = File.ReadAllBytes(files[0]);
        Assert.True(bom.Length >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF);
    }

    [Fact]
    public void StartupCommandLine_ContainsStartupFlag()
    {
        var store = new FakeRunKeyStore();
        var service = new StartupService(store, @"C:\Apps\KeyPulse.exe", "KeyPulse.Test");
        Assert.Contains(LaunchArguments.StartupFlag, service.CommandLine, StringComparison.Ordinal);
        service.Enable();
        Assert.Contains("--startup", store.GetValue("KeyPulse.Test"), StringComparison.Ordinal);
        Assert.StartsWith("\"C:\\Apps\\KeyPulse.exe\"", store.GetValue("KeyPulse.Test"), StringComparison.Ordinal);
    }

    [Fact]
    public void ClearConfirmText_MentionsStatsAndExclusion()
    {
        Assert.Contains("统计数字会删掉", ProductInfo.ClearConfirm, StringComparison.Ordinal);
        Assert.Contains("排除列表可保留", ProductInfo.ClearConfirm, StringComparison.Ordinal);
    }

    private static InputAggregator CreateAggregator() =>
        new(new SilentCapture(), NullLogger<InputAggregator>.Instance);

    private static KeyPressedEvent Key(string name) =>
        new() { Key = new KeyCode(name), Timestamp = DateTimeOffset.Now };

    private sealed class SilentCapture : IInputCapture
    {
        public event EventHandler<InputEvent>? InputReceived
        {
            add { }
            remove { }
        }

        public bool IsListening => true;

        public string? Error => null;

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;
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
