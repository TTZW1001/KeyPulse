using KeyPulse.Core;
using KeyPulse.Core.Events;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Models;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Aggregation;
using KeyPulse.Infrastructure.Input;
using KeyPulse.Infrastructure.Persistence;
using KeyPulse.Infrastructure.Persistence.Repositories;
using KeyPulse.Infrastructure.Query;
using KeyPulse.Infrastructure.System;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KeyPulse.Tests;

public class AppStatsTests : IDisposable
{
    private readonly string _root;
    private readonly AppPaths _paths;
    private readonly SqliteConnectionFactory _factory;
    private readonly StatisticsRepository _repository;

    public AppStatsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "KeyPulseTests", Guid.NewGuid().ToString("N"));
        _paths = new AppPaths(_root);
        _factory = new SqliteConnectionFactory(_paths);
        new DatabaseInitializer(_factory, new MigrationRunner()).Initialize();
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
    public void Resolver_UsesProcessNameAndDescription_NotWindowTitle()
    {
        var native = new FakeNative
        {
            Hwnd = 1,
            ProcessId = 4242,
            ImagePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            FileDescription = "Google Chrome"
        };
        var resolver = new ForegroundAppResolver(native);

        var app = resolver.TryResolve();

        Assert.NotNull(app);
        Assert.Equal("chrome.exe", app.ProcessName);
        Assert.Equal("Google Chrome", app.DisplayName);
        Assert.DoesNotContain('\\', app.ProcessName);
        Assert.DoesNotContain('/', app.ProcessName);
        Assert.DoesNotContain("C:", app.ProcessName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, native.GetForegroundWindowCalls);
        Assert.Equal(1, native.GetWindowThreadProcessIdCalls);
        Assert.Equal(1, native.QueryImagePathCalls);
        Assert.Equal(1, native.QueryFileDescriptionCalls);
        Assert.Equal(0, native.GetWindowTextCalls);
    }

    [Fact]
    public void Resolver_FailedPid_ReturnsNull()
    {
        var native = new FakeNative { Hwnd = 1, ProcessId = 0 };
        Assert.Null(new ForegroundAppResolver(native).TryResolve());
        Assert.Equal(0, native.QueryImagePathCalls);
        Assert.Equal(0, native.GetWindowTextCalls);
    }

    [Fact]
    public void Resolver_CachesDisplayName_WithoutStoringPath()
    {
        var native = new FakeNative
        {
            Hwnd = 1,
            ProcessId = 7,
            ImagePath = @"D:\Apps\Code.exe",
            FileDescription = "Visual Studio Code"
        };
        var resolver = new ForegroundAppResolver(native);
        Assert.Equal("Code.exe", resolver.TryResolve()?.ProcessName);
        Assert.Equal("Code.exe", resolver.TryResolve()?.ProcessName);
        Assert.Equal(1, native.QueryFileDescriptionCalls);
        Assert.Equal("Visual Studio Code", resolver.TryResolve()?.DisplayName);
    }

    [Fact]
    public void ExcludedProcess_NotInAppCounts_GlobalKeysStillCount()
    {
        var cache = new FakeCache { Current = new ForegroundApp("KeePass.exe", "KeePass") };
        var exclusions = new FakeExclusions("KeePass.exe");
        using var aggregator = Create(cache, exclusions);

        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));
        aggregator.Record(new MouseButtonEvent { Timestamp = DateTimeOffset.Now, Button = MouseButton.Left });

        var snap = aggregator.CaptureSnapshot();
        Assert.Equal(2, snap.KeyCounts["A"]);
        Assert.Equal(1, snap.Mouse.Left);
        Assert.Empty(snap.AppCounts);
        Assert.Empty(aggregator.CaptureUnflushed().AppStatsByDate);
    }

    [Fact]
    public void ForegroundCache_FillsAppCounts()
    {
        var cache = new FakeCache { Current = new ForegroundApp("chrome.exe", "Google Chrome") };
        using var aggregator = Create(cache);

        aggregator.Record(Key("A"));
        aggregator.Record(Key("B"));
        aggregator.Record(new MouseButtonEvent { Timestamp = DateTimeOffset.Now, Button = MouseButton.Left });
        aggregator.Record(new MouseWheelEvent { Timestamp = DateTimeOffset.Now, Delta = 120, Horizontal = false });

        var snap = aggregator.CaptureSnapshot();
        Assert.Equal(4, snap.AppCounts["chrome.exe"]);
        var day = aggregator.CaptureUnflushed().AppStatsByDate.Single().Value["chrome.exe"];
        Assert.Equal(2, day.KeyPressCount);
        Assert.Equal(1, day.MouseClickCount);
        Assert.Equal(1, day.WheelEventCount);
        Assert.Equal("Google Chrome", day.DisplayName);
    }

    [Fact]
    public void DefaultExcludedForeground_StillCountsGlobalKeys()
    {
        var cache = new FakeCache { Current = new ForegroundApp("LockApp.exe", "Windows Lock") };
        using var aggregator = Create(cache, new ExcludedAppList(_paths));
        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));
        Assert.Equal(2, aggregator.CaptureSnapshot().KeyCounts["A"]);
        Assert.Empty(aggregator.CaptureSnapshot().AppCounts);
    }

    [Fact]
    public void DefaultExclusions_LockAppAndLogonUi()
    {
        var list = new ExcludedAppList(_paths);
        Assert.True(list.IsExcluded("LockApp.exe"));
        Assert.True(list.IsExcluded("logonui.EXE"));
        Assert.False(list.IsExcluded("chrome.exe"));
        Assert.Equal(new[] { "LockApp.exe", "LogonUI.exe" }, list.Snapshot());
    }

    [Fact]
    public void Exclude_PersistsAndIsCaseInsensitive()
    {
        var list = new ExcludedAppList(_paths);
        list.Exclude("KeePass.exe");
        Assert.True(list.IsExcluded("keepass.EXE"));
        Assert.True(File.Exists(_paths.ExcludedAppsPath));
        var json = File.ReadAllText(_paths.ExcludedAppsPath);
        Assert.Contains("KeePass.exe", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":\\", json, StringComparison.Ordinal);

        var reloaded = new ExcludedAppList(_paths);
        Assert.True(reloaded.IsExcluded("KeePass.exe"));
    }

    [Fact]
    public async Task Flush_WritesAppTables_WithoutPathOrTitle()
    {
        var cache = new FakeCache { Current = new ForegroundApp("chrome.exe", "Google Chrome") };
        using var aggregator = Create(cache);
        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));
        aggregator.Record(new MouseButtonEvent { Timestamp = DateTimeOffset.Now, Button = MouseButton.Left });
        cache.Raise(new ForegroundTick(cache.Current, TimeSpan.FromSeconds(1), DateTimeOffset.Now));

        await _repository.FlushAsync(aggregator.SwapForFlush());

        var today = DateOnly.FromDateTime(DateTime.Now);
        var rows = await _repository.GetAppStatsAsync(today, today);
        var row = Assert.Single(rows);
        Assert.Equal("chrome.exe", row.ProcessName);
        Assert.Equal("Google Chrome", row.DisplayName);
        Assert.Equal(2, row.KeyPressCount);
        Assert.Equal(1, row.MouseClickCount);
        Assert.Equal(1, row.ActiveSeconds);
        Assert.DoesNotContain('\\', row.ProcessName);
        Assert.DoesNotContain("://", row.DisplayName);

        using var connection = _factory.Open();
        AssertNoPrivacyColumns(connection, "app_registry");
        AssertNoPrivacyColumns(connection, "daily_app_stats");
        var process = Scalar(connection, "SELECT process_name FROM app_registry;")?.ToString();
        var display = Scalar(connection, "SELECT display_name FROM app_registry;")?.ToString();
        Assert.Equal("chrome.exe", process);
        Assert.Equal("Google Chrome", display);
        Assert.DoesNotContain(":\\", process);
        Assert.DoesNotContain("http", display, StringComparison.OrdinalIgnoreCase);
        var textDump = Scalar(connection, "SELECT process_name || '|' || IFNULL(display_name,'') FROM app_registry;")?.ToString();
        Assert.DoesNotContain("C:", textDump, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("title", textDump, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Flush_StripsFullPathToFileName()
    {
        var batch = new StatisticsBatch(
            new Dictionary<DateOnly, IReadOnlyDictionary<string, long>>(),
            new Dictionary<DateOnly, MouseTotals>(),
            new Dictionary<HourBucket, HourlyActivity>(),
            new Dictionary<string, long>(),
            new Dictionary<DateOnly, IReadOnlyDictionary<string, AppDayTotals>>
            {
                [DateOnly.FromDateTime(DateTime.Now)] = new Dictionary<string, AppDayTotals>
                {
                    [@"C:\Windows\System32\notepad.exe"] = new(1, 0, 0, 0, 0, "Notepad")
                }
            },
            null);

        await _repository.FlushAsync(batch);
        using var connection = _factory.Open();
        var process = Scalar(connection, "SELECT process_name FROM app_registry;")?.ToString();
        Assert.Equal("notepad.exe", process);
        Assert.NotNull(process);
        Assert.DoesNotContain(":\\", process);
        Assert.DoesNotContain('\\', process);
        var dump = Scalar(
            connection,
            "SELECT group_concat(process_name || '|' || IFNULL(display_name,''), ',') FROM app_registry;")?.ToString();
        Assert.DoesNotContain("Windows", dump, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System32", dump, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Merge_RestoresAppStats_AfterFailedFlush()
    {
        var cache = new FakeCache { Current = new ForegroundApp("Code.exe", "Visual Studio Code") };
        using var aggregator = Create(cache);
        aggregator.Record(Key("A"));
        var batch = aggregator.SwapForFlush();
        Assert.Empty(aggregator.CaptureSnapshot().AppCounts);
        aggregator.Merge(batch);
        Assert.Equal(1, aggregator.CaptureSnapshot().AppCounts["Code.exe"]);
        await _repository.FlushAsync(aggregator.SwapForFlush());
        var today = DateOnly.FromDateTime(DateTime.Now);
        Assert.Equal(1, (await _repository.GetAppStatsAsync(today, today)).Single().KeyPressCount);
    }

    [Fact]
    public async Task Query_MergesDbAndUnflushed()
    {
        var cache = new FakeCache { Current = new ForegroundApp("chrome.exe", "Google Chrome") };
        using var aggregator = Create(cache);
        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));
        await _repository.FlushAsync(aggregator.SwapForFlush());
        aggregator.Record(Key("A"));

        var query = new AppQueryService(_repository, aggregator);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var ranking = await query.GetRankingAsync(today, today);
        var row = Assert.Single(ranking);
        Assert.Equal("chrome.exe", row.ProcessName);
        Assert.Equal("Google Chrome", row.DisplayLabel);
        Assert.Equal(3, row.KeyPressCount);
    }

    [Fact]
    public void AppTables_HaveNoTitleUrlPathColumns()
    {
        using var connection = _factory.Open();
        AssertNoPrivacyColumns(connection, "app_registry");
        AssertNoPrivacyColumns(connection, "daily_app_stats");
        AssertNoPrivacyColumns(connection, "daily_key_stats");
        AssertNoPrivacyColumns(connection, "daily_mouse_stats");
        AssertNoPrivacyColumns(connection, "hourly_activity_stats");
    }

    [Fact]
    public void Source_DoesNotCallGetWindowText()
    {
        var src = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        Assert.True(Directory.Exists(src), src);
        var hits = new List<string>();
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            if (text.Contains("GetWindowText", StringComparison.Ordinal) ||
                text.Contains("GetWindowTextLength", StringComparison.Ordinal) ||
                text.Contains("MainWindowTitle", StringComparison.Ordinal) ||
                text.Contains("System.Windows.Automation", StringComparison.Ordinal))
            {
                hits.Add(Path.GetRelativePath(src, file));
            }
        }

        Assert.True(hits.Count == 0, "privacy API used in: " + string.Join(", ", hits));
    }

    [Fact]
    public void Sampler_EmitsPreviousAppElapsed_WithFakeClock()
    {
        var resolver = new StubResolver(new ForegroundApp("chrome.exe", "Google Chrome"));
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
        using var sampler = new ForegroundAppSampler(resolver, clock, NullLogger<ForegroundAppSampler>.Instance);
        ForegroundTick? tick = null;
        sampler.Sampled += t => tick = t;

        sampler.SampleOnce();
        Assert.Equal("chrome.exe", sampler.Current?.ProcessName);
        Assert.Null(tick);

        clock.Now = clock.Now.AddSeconds(1);
        sampler.SampleOnce();
        Assert.NotNull(tick);
        Assert.Equal("chrome.exe", tick.App?.ProcessName);
        Assert.Equal(TimeSpan.FromSeconds(1), tick.Elapsed);
    }

    private static InputAggregator Create(
        IForegroundAppCache? cache = null,
        IExcludedAppList? exclusions = null) =>
        new(new SilentCapture(), NullLogger<InputAggregator>.Instance, cache, exclusions);

    private static KeyPressedEvent Key(string name) =>
        new() { Key = new KeyCode(name), Timestamp = DateTimeOffset.Now };

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void AssertNoPrivacyColumns(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(" + table + ");";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(1));
        }

        Assert.DoesNotContain(names, name =>
            name.Contains("title", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("url", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("path", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("command", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("clipboard", StringComparison.OrdinalIgnoreCase));
    }

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

    private sealed class FakeNative : IForegroundProcessNative
    {
        public nint Hwnd { get; set; }
        public uint ProcessId { get; set; }
        public string? ImagePath { get; set; }
        public string? FileDescription { get; set; }
        public int GetForegroundWindowCalls { get; private set; }
        public int GetWindowThreadProcessIdCalls { get; private set; }
        public int QueryImagePathCalls { get; private set; }
        public int QueryFileDescriptionCalls { get; private set; }
        public int GetWindowTextCalls { get; private set; }

        public nint GetForegroundWindow()
        {
            GetForegroundWindowCalls++;
            return Hwnd;
        }

        public uint GetWindowThreadProcessId(nint hwnd, out uint processId)
        {
            GetWindowThreadProcessIdCalls++;
            processId = ProcessId;
            return 1;
        }

        public string? QueryImagePath(uint processId)
        {
            QueryImagePathCalls++;
            return ImagePath;
        }

        public string? QueryFileDescription(string imagePath)
        {
            QueryFileDescriptionCalls++;
            return FileDescription;
        }
    }

    private sealed class FakeCache : IForegroundAppCache
    {
        public ForegroundApp? Current { get; set; }

        public event Action<ForegroundTick>? Sampled;

        public void Raise(ForegroundTick tick) => Sampled?.Invoke(tick);
    }

    private sealed class FakeExclusions : IExcludedAppList
    {
        private readonly HashSet<string> _names;

        public FakeExclusions(params string[] names)
        {
            _names = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        }

        public event Action? Changed
        {
            add { }
            remove { }
        }

        public bool IsExcluded(string processName) => _names.Contains(processName);

        public IReadOnlyList<string> Snapshot() => _names.ToList();

        public void Exclude(string processName) => _names.Add(processName);
    }

    private sealed class StubResolver : IForegroundAppResolver
    {
        private readonly ForegroundApp? _app;

        public StubResolver(ForegroundApp? app) => _app = app;

        public ForegroundApp? TryResolve() => _app;
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset now) => Now = now;

        public DateTimeOffset Now { get; set; }
    }
}
