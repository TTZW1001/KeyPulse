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

public class DashboardQueryTests : IDisposable
{
    private readonly string _root;
    private readonly AppPaths _paths;
    private readonly SqliteConnectionFactory _factory;
    private readonly StatisticsRepository _repository;

    public DashboardQueryTests()
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
    public async Task Merge_DbPlusUnflushedSnapshot()
    {
        using var aggregator = CreateAggregator();
        var today = DateOnly.FromDateTime(DateTime.Now);
        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));
        await _repository.FlushAsync(aggregator.SwapForFlush());

        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));

        var query = new DashboardQueryService(_repository, aggregator);
        var result = await query.GetTodayAsync(today);

        Assert.Equal(8, result.KeyPressCount);
        Assert.Equal("A", result.TopKey);
        Assert.Equal(8, result.TopKeyCount);
    }

    [Fact]
    public async Task AfterFlush_TodayStillIncludesPersisted()
    {
        using var aggregator = CreateAggregator();
        var today = DateOnly.FromDateTime(DateTime.Now);
        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));
        aggregator.Record(Key("Space"));
        await _repository.FlushAsync(aggregator.SwapForFlush());

        var query = new DashboardQueryService(_repository, aggregator);
        var result = await query.GetTodayAsync(today);

        Assert.Equal(0, aggregator.CaptureSnapshot().KeyCounts.GetValueOrDefault("A"));
        Assert.Equal(3, result.KeyPressCount);
        Assert.Equal("A", result.TopKey);
        Assert.Equal(2, result.TopKeyCount);
    }

    [Fact]
    public async Task Last7Days_LengthIs7_IncludesTodayUnflushed()
    {
        using var aggregator = CreateAggregator();
        var today = DateOnly.FromDateTime(DateTime.Now);
        aggregator.Record(Key("A"));
        var query = new DashboardQueryService(_repository, aggregator);
        var days = await query.GetLast7DaysAsync(today);

        Assert.Equal(7, days.Count);
        Assert.Equal(today.AddDays(-6), days[0].Date);
        Assert.Equal(today, days[6].Date);
        Assert.Equal(1, days[6].KeyPressCount);
        Assert.All(days.Take(6), point => Assert.Equal(0, point.KeyPressCount));
    }

    [Fact]
    public async Task Hourly_LengthIs24_MergesUnflushed()
    {
        using var aggregator = CreateAggregator();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var noon = new DateTime(today.Year, today.Month, today.Day, 12, 0, 0, DateTimeKind.Local);
        aggregator.Record(Key("A", new DateTimeOffset(noon)));
        aggregator.Record(new MouseButtonEvent
        {
            Timestamp = new DateTimeOffset(noon),
            Button = MouseButton.Left
        });

        var query = new DashboardQueryService(_repository, aggregator);
        var hours = await query.GetTodayHourlyAsync(today);

        Assert.Equal(24, hours.Count);
        Assert.Equal(Enumerable.Range(0, 24), hours.Select(point => point.Hour));
        Assert.Equal(2, hours[12].ActivityCount);
        Assert.Equal(1, hours[12].KeyPressCount);
        Assert.Equal(1, hours[12].MouseClickCount);
        Assert.Equal(0, hours[12].WheelEventCount);
        Assert.Equal(0, hours[0].ActivityCount);
    }

    [Fact]
    public async Task EmptyData_StillReturnsZeroFilledSeries()
    {
        using var aggregator = CreateAggregator();
        var query = new DashboardQueryService(_repository, aggregator);
        var today = await query.GetTodayAsync();
        var days = await query.GetLast7DaysAsync();
        var hours = await query.GetTodayHourlyAsync();

        Assert.Equal(0, today.KeyPressCount);
        Assert.Null(today.TopKey);
        Assert.Equal(7, days.Count);
        Assert.Equal(24, hours.Count);
        Assert.All(days, point => Assert.Equal(0, point.KeyPressCount));
        Assert.All(hours, point => Assert.Equal(0, point.ActivityCount));
    }

    [Fact]
    public async Task InvalidHistoricalKeyCodes_AreExcludedFromDashboardTotals()
    {
        using var aggregator = CreateAggregator();
        var today = DateOnly.FromDateTime(DateTime.Now);
        aggregator.Record(Key("VK_FF"));
        await _repository.FlushAsync(aggregator.SwapForFlush());

        var result = await new DashboardQueryService(_repository, aggregator).GetTodayAsync(today);

        Assert.Equal(0, result.KeyPressCount);
        Assert.Null(result.TopKey);
    }

    private static InputAggregator CreateAggregator() =>
        new(new SilentCapture(), NullLogger<InputAggregator>.Instance);

    private static KeyPressedEvent Key(string name, DateTimeOffset? timestamp = null) =>
        new()
        {
            Key = new KeyCode(name),
            Timestamp = timestamp ?? DateTimeOffset.Now
        };

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
}
