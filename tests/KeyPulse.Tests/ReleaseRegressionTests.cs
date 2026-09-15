using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Persistence;
using KeyPulse.Infrastructure.Persistence.Repositories;
using KeyPulse.Infrastructure.Query;
using KeyPulse.Infrastructure.System;
using Microsoft.Data.Sqlite;
using Xunit;

namespace KeyPulse.Tests;

public sealed class ReleaseRegressionTests : IDisposable
{
    private readonly string _root;
    private readonly AppPaths _paths;
    private readonly SqliteConnectionFactory _factory;
    private readonly DatabaseInitializer _initializer;
    private readonly StatisticsRepository _repository;

    public ReleaseRegressionTests()
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
    public void Migration_IsIdempotent_AndKeepsSingleVersionRow()
    {
        using var before = _factory.Open();
        var createdAt = Scalar(before, "SELECT meta_value FROM settings_meta WHERE meta_key = 'created_at';");
        before.Close();

        _initializer.Initialize();

        using var after = _factory.Open();
        Assert.Equal(1L, ScalarLong(after, "SELECT COUNT(*) FROM schema_version;"));
        Assert.Equal(1L, ScalarLong(after, "SELECT version FROM schema_version WHERE id = 1;"));
        Assert.Equal(createdAt, Scalar(after, "SELECT meta_value FROM settings_meta WHERE meta_key = 'created_at';"));
    }

    [Fact]
    public async Task RepeatedFlush_AccumulatesEveryPersistedAggregate()
    {
        var date = new DateOnly(2026, 9, 15);
        var hour = new HourBucket(date, 21);
        await _repository.FlushAsync(Batch(
            date,
            hour,
            keyCount: 2,
            mouse: new MouseTotals(1, 2, 3, 4, 5, 6, 7, 8, 9, 10.5),
            hourly: new HourlyActivity(2, 3, 4, 10.5),
            app: new AppDayTotals(2, 3, 4, 10.5, 5, "Test App")));
        await _repository.FlushAsync(Batch(
            date,
            hour,
            keyCount: 3,
            mouse: new MouseTotals(10, 20, 30, 40, 50, 60, 70, 80, 90, 1.5),
            hourly: new HourlyActivity(3, 4, 5, 1.5),
            app: new AppDayTotals(3, 4, 5, 1.5, 7, "Test App Updated")));

        Assert.Equal(5, Assert.Single(await _repository.GetKeyStatsAsync(date, date)).PressCount);

        var mouse = Assert.Single(await _repository.GetMouseStatsAsync(date, date)).Mouse;
        Assert.Equal(new MouseTotals(11, 22, 33, 44, 55, 66, 77, 88, 99, 12), mouse);

        var hourly = Assert.Single(await _repository.GetHourlyAsync(date, date));
        Assert.Equal((5L, 7L, 9L, 12D),
            (hourly.KeyPressCount, hourly.MouseClickCount, hourly.WheelEventCount, hourly.MouseDistancePixels));

        var app = Assert.Single(await _repository.GetAppStatsAsync(date, date));
        Assert.Equal("test-app.exe", app.ProcessName);
        Assert.Equal("Test App Updated", app.DisplayName);
        Assert.Equal((5L, 7L, 9L, 12D, 12L),
            (app.KeyPressCount, app.MouseClickCount, app.WheelEventCount, app.MouseDistancePixels, app.ActiveSeconds));
    }

    [Fact]
    public async Task CsvExport_EscapesCommaAndQuote_InAppLabels()
    {
        var date = new DateOnly(2026, 9, 15);
        var batch = new StatisticsBatch(
            new Dictionary<DateOnly, IReadOnlyDictionary<string, long>>(),
            new Dictionary<DateOnly, MouseTotals>(),
            new Dictionary<HourBucket, HourlyActivity>(),
            new Dictionary<string, long>(),
            new Dictionary<DateOnly, IReadOnlyDictionary<string, AppDayTotals>>
            {
                [date] = new Dictionary<string, AppDayTotals>
                {
                    ["comma,app.exe"] = new(1, 2, 3, 4.5, 6, "Quoted \"App\", Suite")
                }
            },
            null);
        await _repository.FlushAsync(batch);

        var files = await new StatisticsExportService(_factory)
            .ExportCsvAsync(Path.Combine(_root, "export"));
        var appFile = files.Single(path => path.Contains("daily-app-stats", StringComparison.Ordinal));
        var rows = File.ReadAllLines(appFile);

        Assert.Equal(2, rows.Length);
        Assert.Equal(
            "2026-09-15,\"comma,app.exe\",\"Quoted \"\"App\"\", Suite\",1,2,3,4.5,6",
            rows[1]);
    }

    private static StatisticsBatch Batch(
        DateOnly date,
        HourBucket hour,
        long keyCount,
        MouseTotals mouse,
        HourlyActivity hourly,
        AppDayTotals app) =>
        new(
            new Dictionary<DateOnly, IReadOnlyDictionary<string, long>>
            {
                [date] = new Dictionary<string, long> { ["A"] = keyCount }
            },
            new Dictionary<DateOnly, MouseTotals> { [date] = mouse },
            new Dictionary<HourBucket, HourlyActivity> { [hour] = hourly },
            new Dictionary<string, long>(),
            new Dictionary<DateOnly, IReadOnlyDictionary<string, AppDayTotals>>
            {
                [date] = new Dictionary<string, AppDayTotals> { ["test-app.exe"] = app }
            },
            null);

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static long ScalarLong(SqliteConnection connection, string sql) =>
        Convert.ToInt64(Scalar(connection, sql));
}
