using KeyPulse.Core.Events;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Models;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Aggregation;
using KeyPulse.Infrastructure.Persistence;
using KeyPulse.Infrastructure.Persistence.Repositories;
using KeyPulse.Infrastructure.System;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KeyPulse.Tests;

public class PersistenceTests : IDisposable
{
    private readonly string _root;
    private readonly AppPaths _paths;
    private readonly SqliteConnectionFactory _factory;
    private readonly DatabaseInitializer _initializer;
    private readonly StatisticsRepository _repository;

    public PersistenceTests()
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
    public void Migration_CreatesExpectedTablesAndWal()
    {
        using var connection = _factory.Open();
        var tables = ReadStrings(connection, "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;");
        Assert.Contains("schema_version", tables);
        Assert.Contains("daily_key_stats", tables);
        Assert.Contains("daily_mouse_stats", tables);
        Assert.Contains("hourly_activity_stats", tables);
        Assert.Contains("daily_app_stats", tables);
        Assert.Contains("app_registry", tables);
        Assert.Contains("settings_meta", tables);
        Assert.DoesNotContain("keyboard_events", tables);
        Assert.DoesNotContain("input_events", tables);
        Assert.DoesNotContain("key_sequence", tables);
        Assert.DoesNotContain("typed_text", tables);
        Assert.DoesNotContain("window_titles", tables);

        Assert.Equal(1, Convert.ToInt32(Scalar(connection, "SELECT version FROM schema_version WHERE id = 1;")));
        Assert.Equal("wal", Scalar(connection, "PRAGMA journal_mode;")?.ToString()?.ToLowerInvariant());
    }

    [Fact]
    public async Task TwoFlushes_SameKey_Accumulate()
    {
        using var aggregator = CreateAggregator();
        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));
        await _repository.FlushAsync(aggregator.SwapForFlush());

        aggregator.Record(Key("A"));
        await _repository.FlushAsync(aggregator.SwapForFlush());

        var today = DateOnly.FromDateTime(DateTime.Now);
        var rows = await _repository.GetKeyStatsAsync(today, today);
        Assert.Equal(3, rows.Single(row => row.KeyCode == "A").PressCount);
    }

    [Fact]
    public async Task MidnightBatch_WritesTwoDates()
    {
        using var aggregator = CreateAggregator();
        aggregator.Record(Key("A", Local(2026, 9, 15, 23, 59)));
        aggregator.Record(Key("A", Local(2026, 9, 16, 0, 1)));
        await _repository.FlushAsync(aggregator.SwapForFlush());

        var rows = await _repository.GetKeyStatsAsync(new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 16));
        Assert.Equal(1, rows.Single(row => row.Date == new DateOnly(2026, 9, 15)).PressCount);
        Assert.Equal(1, rows.Single(row => row.Date == new DateOnly(2026, 9, 16)).PressCount);

        var hours = await _repository.GetHourlyAsync(new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 16));
        Assert.Contains(hours, row => row.Date == new DateOnly(2026, 9, 15) && row.Hour == 23 && row.ActiveSeconds == 0);
        Assert.Contains(hours, row => row.Date == new DateOnly(2026, 9, 16) && row.Hour == 0 && row.ActiveSeconds == 0);
    }

    [Fact]
    public async Task EmptyBatch_DoesNotCorruptDatabase()
    {
        await _repository.FlushAsync(new StatisticsBatch(
            new Dictionary<DateOnly, IReadOnlyDictionary<string, long>>(),
            new Dictionary<DateOnly, MouseTotals>(),
            new Dictionary<HourBucket, HourlyActivity>(),
            new Dictionary<string, long>(),
            null));

        using var connection = _factory.Open();
        Assert.Equal(0, Convert.ToInt32(Scalar(connection, "SELECT COUNT(*) FROM daily_key_stats;")));
        Assert.Equal(1, Convert.ToInt32(Scalar(connection, "SELECT version FROM schema_version WHERE id = 1;")));
    }

    [Fact]
    public async Task FlushThenReopen_StillHasCounts()
    {
        using (var aggregator = CreateAggregator())
        {
            aggregator.Record(Key("Space"));
            aggregator.Record(new MouseButtonEvent
            {
                Timestamp = DateTimeOffset.Now,
                Button = MouseButton.Left
            });
            await _repository.FlushAsync(aggregator.SwapForFlush());
        }

        var reopened = new StatisticsRepository(new SqliteConnectionFactory(_paths));
        var today = DateOnly.FromDateTime(DateTime.Now);
        var keys = await reopened.GetKeyStatsAsync(today, today);
        var mouse = await reopened.GetMouseStatsAsync(today, today);
        Assert.Equal(1, keys.Single(row => row.KeyCode == "Space").PressCount);
        Assert.Equal(1, mouse.Single().Mouse.Left);
    }

    [Fact]
    public async Task FlushFailure_MergesBatchBack()
    {
        using var aggregator = CreateAggregator();
        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));

        var flush = new FlushService(
            aggregator,
            new ThrowingRepository(),
            _initializer,
            new PersistenceOptions(),
            NullLogger<FlushService>.Instance);

        await flush.FlushNowAsync();
        Assert.Equal(2, aggregator.CaptureSnapshot().KeyCounts["A"]);
    }

    [Fact]
    public async Task DefaultDatabase_WhenRequested_HasFlushedKeys()
    {
        if (Environment.GetEnvironmentVariable("KEYPULSE_CHECK_DEFAULT_DB") != "1")
        {
            return;
        }

        var paths = new AppPaths();
        Assert.True(File.Exists(paths.DatabasePath), "expected default keypulse.db after live run");
        var repository = new StatisticsRepository(new SqliteConnectionFactory(paths));
        var today = DateOnly.FromDateTime(DateTime.Now);
        var keys = await repository.GetKeyStatsAsync(today, today);
        Assert.True(keys.Sum(row => row.PressCount) >= 1, "expected persisted key counts after exit flush");
    }

    private static InputAggregator CreateAggregator() =>
        new(new SilentCapture(), NullLogger<InputAggregator>.Instance);

    private static KeyPressedEvent Key(string name, DateTimeOffset? timestamp = null) =>
        new()
        {
            Key = new KeyCode(name),
            Timestamp = timestamp ?? DateTimeOffset.Now
        };

    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute)
    {
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Local);
        return new DateTimeOffset(local);
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static List<string> ReadStrings(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read())
        {
            values.Add(reader.GetString(0));
        }

        return values;
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

    private sealed class ThrowingRepository : IStatisticsRepository
    {
        public Task FlushAsync(StatisticsBatch batch, CancellationToken cancellationToken = default) =>
            throw new IOException("simulated flush failure");

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

        public Task<IReadOnlyList<DailyKeyRow>> GetAppStatsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DailyKeyRow>>(Array.Empty<DailyKeyRow>());

        public Task<DateOnly?> GetEarliestStatDateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DateOnly?>(null);
    }
}
