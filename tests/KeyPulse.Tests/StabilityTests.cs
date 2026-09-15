using KeyPulse.Core;
using KeyPulse.Core.Events;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Models;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Aggregation;
using KeyPulse.Infrastructure.Hosting;
using KeyPulse.Infrastructure.Persistence;
using KeyPulse.Infrastructure.Persistence.Repositories;
using KeyPulse.Infrastructure.System;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KeyPulse.Tests;

public class StabilityTests : IDisposable
{
    private readonly string _root;
    private readonly AppPaths _paths;

    public StabilityTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "KeyPulseTests", Guid.NewGuid().ToString("N"));
        _paths = new AppPaths(_root);
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
    public async Task Resume_RestartsCapture_WhenNotListening()
    {
        var capture = new FakeCapture { IsListening = false };
        var flush = new CountingFlush();
        var cache = new FakeCache();
        using var aggregator = CreateAggregator();
        aggregator.SetState(TrackingState.Error);
        var recovery = new LifecycleRecovery(
            capture, flush, aggregator, NullLogger<LifecycleRecovery>.Instance, cache);

        await recovery.OnResumeAsync();

        Assert.Equal(1, capture.StartCalls);
        Assert.True(capture.IsListening);
        Assert.Equal(TrackingState.Running, aggregator.State);
        Assert.Equal(0, flush.FlushCalls);
        Assert.Equal(1, cache.RefreshCalls);
    }

    [Fact]
    public async Task Resume_DoesNotStart_WhenAlreadyListening()
    {
        var capture = new FakeCapture { IsListening = true };
        using var aggregator = CreateAggregator();
        var recovery = new LifecycleRecovery(
            capture, new CountingFlush(), aggregator, NullLogger<LifecycleRecovery>.Instance);

        await recovery.OnResumeAsync();
        Assert.Equal(0, capture.StartCalls);
        Assert.True(capture.IsListening);
    }

    [Fact]
    public async Task SessionEnding_StopsCapture_AndFlushes()
    {
        var capture = new FakeCapture { IsListening = true };
        var flush = new CountingFlush();
        using var aggregator = CreateAggregator();
        var recovery = new LifecycleRecovery(
            capture, flush, aggregator, NullLogger<LifecycleRecovery>.Instance);

        await recovery.OnSessionEndingAsync();
        Assert.Equal(1, capture.StopCalls);
        Assert.False(capture.IsListening);
        Assert.Equal(1, flush.FlushCalls);
    }

    [Fact]
    public async Task Suspend_Flushes_DoesNotStopCapture()
    {
        var capture = new FakeCapture { IsListening = true };
        var flush = new CountingFlush();
        using var aggregator = CreateAggregator();
        var recovery = new LifecycleRecovery(
            capture, flush, aggregator, NullLogger<LifecycleRecovery>.Instance);

        await recovery.OnSuspendAsync();
        Assert.Equal(0, capture.StopCalls);
        Assert.True(capture.IsListening);
        Assert.Equal(1, flush.FlushCalls);
    }

    [Fact]
    public void TaskbarCreated_RecreatesOncePerMessage_IgnoresOthers()
    {
        var router = new TaskbarCreatedRouter(0xC000);
        var hits = 0;
        router.RecreateRequested += () => hits++;

        Assert.True(router.TryHandle(0xC000));
        Assert.False(router.TryHandle(0x0010));
        Assert.True(router.TryHandle(unchecked((int)0xC000)));
        Assert.Equal(2, router.RecreateCount);
        Assert.Equal(2, hits);
    }

    [Fact]
    public void CorruptSettingsJson_FallsBackToDefaultTheme()
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        File.WriteAllText(_paths.SettingsPath, "{ this is not json");
        var settings = new JsonUserSettings(_paths);
        Assert.Equal(ThemeMode.System, settings.Theme);
        Assert.False(settings.HideToTrayHintDismissed);
    }

    [Fact]
    public void CorruptExcludedAppsJson_KeepsDefaults()
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        File.WriteAllText(_paths.ExcludedAppsPath, "[[[[");
        var list = new ExcludedAppList(_paths);
        Assert.True(list.IsExcluded("LockApp.exe"));
        Assert.True(list.IsExcluded("LogonUI.exe"));
        Assert.False(list.IsExcluded("chrome.exe"));
    }

    [Fact]
    public async Task FlushFailure_SetsWriteError_AndMergesBatch()
    {
        var factory = new SqliteConnectionFactory(_paths);
        var initializer = new DatabaseInitializer(factory, new MigrationRunner());
        using var aggregator = CreateAggregator();
        aggregator.Record(new KeyPressedEvent { Key = new KeyCode("A"), Timestamp = DateTimeOffset.Now });

        var flush = new FlushService(
            aggregator,
            new ThrowingStats(),
            initializer,
            new PersistenceOptions(),
            NullLogger<FlushService>.Instance);

        await flush.FlushNowAsync();
        Assert.True(flush.HasWriteError);
        Assert.Equal(1, aggregator.CaptureSnapshot().KeyCounts["A"]);
    }

    [Fact]
    public async Task FlushSuccess_ClearsWriteError()
    {
        var factory = new SqliteConnectionFactory(_paths);
        var initializer = new DatabaseInitializer(factory, new MigrationRunner());
        initializer.Initialize();
        var repository = new StatisticsRepository(factory);
        using var aggregator = CreateAggregator();
        aggregator.Record(new KeyPressedEvent { Key = new KeyCode("A"), Timestamp = DateTimeOffset.Now });

        var flush = new FlushService(
            aggregator,
            repository,
            initializer,
            new PersistenceOptions(),
            NullLogger<FlushService>.Instance);

        await flush.FlushNowAsync();
        Assert.False(flush.HasWriteError);
        Assert.Empty(aggregator.CaptureSnapshot().KeyCounts);
    }

    [Fact]
    public async Task InitializeFailure_RetriesBeforeWritingPendingBatch()
    {
        File.WriteAllText(_root, "temporarily blocking data directory creation");
        var factory = new SqliteConnectionFactory(_paths);
        var initializer = new DatabaseInitializer(factory, new MigrationRunner());
        var repository = new StatisticsRepository(factory);
        using var aggregator = CreateAggregator();
        aggregator.Record(new KeyPressedEvent { Key = new KeyCode("A"), Timestamp = DateTimeOffset.Now });
        using var flush = new FlushService(
            aggregator,
            repository,
            initializer,
            new PersistenceOptions(),
            NullLogger<FlushService>.Instance);

        await flush.StartAsync(CancellationToken.None);
        Assert.True(flush.HasWriteError);

        File.Delete(_root);
        await flush.FlushNowAsync();

        Assert.False(flush.HasWriteError);
        Assert.Empty(aggregator.CaptureSnapshot().KeyCounts);
        Assert.Single(await repository.GetKeyStatsAsync(
            DateOnly.FromDateTime(DateTime.Now),
            DateOnly.FromDateTime(DateTime.Now)));
    }

    [Fact]
    public async Task FlushedSubscriberFailure_DoesNotMergeCommittedBatch()
    {
        var factory = new SqliteConnectionFactory(_paths);
        var initializer = new DatabaseInitializer(factory, new MigrationRunner());
        initializer.Initialize();
        var repository = new StatisticsRepository(factory);
        using var aggregator = CreateAggregator();
        aggregator.Record(new KeyPressedEvent { Key = new KeyCode("A"), Timestamp = DateTimeOffset.Now });
        using var flush = new FlushService(
            aggregator,
            repository,
            initializer,
            new PersistenceOptions(),
            NullLogger<FlushService>.Instance);
        flush.Flushed += () => throw new InvalidOperationException("subscriber failed");

        await flush.FlushNowAsync();

        Assert.False(flush.HasWriteError);
        Assert.Empty(aggregator.CaptureSnapshot().KeyCounts);
        var rows = await repository.GetKeyStatsAsync(
            DateOnly.FromDateTime(DateTime.Now),
            DateOnly.FromDateTime(DateTime.Now));
        Assert.Equal(1, Assert.Single(rows).PressCount);
    }

    [Fact]
    public void WriteErrorCopy_IsUserVisiblePhrase()
    {
        Assert.Equal("无法写入统计数据", StatusCopy.WriteError);
    }

    private static InputAggregator CreateAggregator() =>
        new(new SilentCapture(), NullLogger<InputAggregator>.Instance);

    private sealed class FakeCapture : IInputCapture
    {
        public event EventHandler<InputEvent>? InputReceived
        {
            add { }
            remove { }
        }

        public int StartCalls { get; private set; }

        public int StopCalls { get; private set; }

        public bool IsListening { get; set; }

        public string? Error { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalls++;
            IsListening = true;
            Error = null;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopCalls++;
            IsListening = false;
            return Task.CompletedTask;
        }
    }

    private sealed class CountingFlush : IFlushService
    {
        public int FlushCalls { get; private set; }

        public event Action? Flushed
        {
            add { }
            remove { }
        }

        public event Action? WriteErrorChanged
        {
            add { }
            remove { }
        }

        public bool HasWriteError => false;

        public Task FlushNowAsync(CancellationToken cancellationToken = default)
        {
            FlushCalls++;
            return Task.CompletedTask;
        }

        public Task ClearStatisticsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeCache : IForegroundAppCache
    {
        public int RefreshCalls { get; private set; }

        public ForegroundApp? Current => null;

        public event Action<ForegroundTick>? Sampled
        {
            add { }
            remove { }
        }

        public void RefreshSample() => RefreshCalls++;
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

    private sealed class ThrowingStats : IStatisticsRepository
    {
        public Task FlushAsync(StatisticsBatch batch, CancellationToken cancellationToken = default) =>
            throw new IOException("disk full");

        public Task<IReadOnlyList<DailyKeyRow>> GetKeyStatsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DailyKeyRow>>(Array.Empty<DailyKeyRow>());

        public Task<IReadOnlyList<DailyMouseRow>> GetMouseStatsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DailyMouseRow>>(Array.Empty<DailyMouseRow>());

        public Task<IReadOnlyList<HourlyRow>> GetHourlyAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<HourlyRow>>(Array.Empty<HourlyRow>());

        public Task<DashboardSummary> GetDashboardAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DashboardSummary(date, 0, 0, 0));

        public Task<IReadOnlyList<DailyKeyRow>> GetTrendAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DailyKeyRow>>(Array.Empty<DailyKeyRow>());

        public Task<IReadOnlyList<DailyAppRow>> GetAppStatsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DailyAppRow>>(Array.Empty<DailyAppRow>());

        public Task<DateOnly?> GetEarliestStatDateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DateOnly?>(null);

        public Task ClearStatisticsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public bool TryPing() => false;
    }
}
