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

public class TrendQueryTests : IDisposable
{
    private readonly string _root;
    private readonly StatisticsRepository _repository;

    public TrendQueryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "KeyPulseTests", Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(_root);
        var factory = new SqliteConnectionFactory(paths);
        new DatabaseInitializer(factory, new MigrationRunner()).Initialize();
        _repository = new StatisticsRepository(factory);
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
    public void Resolver_Last7Days_Has7Days()
    {
        var today = new DateOnly(2026, 9, 15);
        var (from, to) = TrendRangeResolver.Resolve(TrendRangeKind.Last7Days, today, null, null, null);
        Assert.Equal(today.AddDays(-6), from);
        Assert.Equal(today, to);
        Assert.Equal(7, to.DayNumber - from.DayNumber + 1);
    }

    [Fact]
    public void Resolver_Custom_SwapsWhenFromAfterTo()
    {
        var today = new DateOnly(2026, 9, 15);
        var (from, to) = TrendRangeResolver.Resolve(
            TrendRangeKind.Custom,
            today,
            null,
            new DateOnly(2026, 9, 20),
            new DateOnly(2026, 9, 10));
        Assert.Equal(new DateOnly(2026, 9, 10), from);
        Assert.Equal(new DateOnly(2026, 9, 20), to);
    }

    [Fact]
    public void Resolver_Custom_ClampsTo366Days()
    {
        var today = new DateOnly(2026, 9, 15);
        var (from, to) = TrendRangeResolver.Resolve(
            TrendRangeKind.Custom,
            today,
            null,
            new DateOnly(2024, 1, 1),
            today);
        Assert.Equal(today, to);
        Assert.Equal(TrendRangeResolver.MaxCustomSpanDays, to.DayNumber - from.DayNumber + 1);
    }

    [Fact]
    public void Resolver_All_EmptyEarliest_IsToday()
    {
        var today = new DateOnly(2026, 9, 15);
        var (from, to) = TrendRangeResolver.Resolve(TrendRangeKind.All, today, null, null, null);
        Assert.Equal(today, from);
        Assert.Equal(today, to);
    }

    [Fact]
    public void Resolver_All_UsesEarliest()
    {
        var today = new DateOnly(2026, 9, 15);
        var earliest = new DateOnly(2026, 8, 1);
        var (from, to) = TrendRangeResolver.Resolve(TrendRangeKind.All, today, earliest, null, null);
        Assert.Equal(earliest, from);
        Assert.Equal(today, to);
    }

    [Fact]
    public void Resolver_ThisMonth_StartsOnFirst()
    {
        var today = new DateOnly(2026, 9, 15);
        var (from, to) = TrendRangeResolver.Resolve(TrendRangeKind.ThisMonth, today, null, null, null);
        Assert.Equal(new DateOnly(2026, 9, 1), from);
        Assert.Equal(today, to);
    }

    [Fact]
    public async Task Daily_7Days_FillsMissingWithZero()
    {
        using var aggregator = CreateAggregator();
        var today = DateOnly.FromDateTime(DateTime.Now);
        aggregator.Record(Key("A"));
        var query = new TrendQueryService(_repository, aggregator);
        var days = await query.GetDailyAsync(today.AddDays(-6), today);

        Assert.Equal(7, days.Count);
        Assert.Equal(today.AddDays(-6), days[0].Date);
        Assert.Equal(today, days[6].Date);
        Assert.Equal(1, days[6].KeyPressCount);
        Assert.All(days.Take(6), point => Assert.Equal(0, point.KeyPressCount));
    }

    [Fact]
    public async Task Daily_Today_ExcludesYesterday()
    {
        using var aggregator = CreateAggregator();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);
        aggregator.Record(Key("A", At(yesterday, 18, 0)));
        aggregator.Record(Key("A", At(today, 10, 0)));
        await _repository.FlushAsync(aggregator.SwapForFlush());
        aggregator.Record(Key("A", At(today, 10, 1)));

        var query = new TrendQueryService(_repository, aggregator);
        var todayDays = await query.GetDailyAsync(today, today);
        var twoDays = await query.GetDailyAsync(yesterday, today);

        var todayPoint = Assert.Single(todayDays);
        Assert.Equal(2, todayPoint.KeyPressCount);
        Assert.Equal(2, twoDays.Count);
        Assert.Equal(1, twoDays[0].KeyPressCount);
        Assert.Equal(2, twoDays[1].KeyPressCount);
    }

    [Fact]
    public async Task Hourly_LengthIs24_SumsRange()
    {
        using var aggregator = CreateAggregator();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var noon = today.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Local);
        aggregator.Record(Key("A", new DateTimeOffset(noon)));
        aggregator.Record(new MouseButtonEvent
        {
            Timestamp = new DateTimeOffset(noon),
            Button = MouseButton.Left
        });

        var query = new TrendQueryService(_repository, aggregator);
        var hours = await query.GetHourlyAsync(today, today);
        Assert.Equal(24, hours.Count);
        Assert.Equal(Enumerable.Range(0, 24), hours.Select(h => h.Hour));
        Assert.Equal(2, hours[12].ActivityCount);
    }

    [Fact]
    public async Task Empty_DoesNotThrow_AllIsSingleDay()
    {
        using var aggregator = CreateAggregator();
        var query = new TrendQueryService(_repository, aggregator);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var earliest = await query.GetEarliestDateAsync();
        Assert.Null(earliest);
        var (from, to) = TrendRangeResolver.Resolve(TrendRangeKind.All, today, earliest, null, null);
        var days = await query.GetDailyAsync(from, to);
        var hours = await query.GetHourlyAsync(from, to);
        var only = Assert.Single(days);
        Assert.Equal(0, only.KeyPressCount);
        Assert.Equal(24, hours.Count);
        Assert.All(hours, h => Assert.Equal(0, h.ActivityCount));
    }

    [Fact]
    public async Task Custom_CrossMonth_AndAllUsesEarliest()
    {
        using var aggregator = CreateAggregator();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var lastMonth = today.AddMonths(-1);
        aggregator.Record(Key("A", At(lastMonth, 8, 0)));
        await _repository.FlushAsync(aggregator.SwapForFlush());
        aggregator.Record(Key("A"));

        var query = new TrendQueryService(_repository, aggregator);
        var earliest = await query.GetEarliestDateAsync();
        Assert.Equal(lastMonth, earliest);

        var (from, to) = TrendRangeResolver.Resolve(TrendRangeKind.All, today, earliest, null, null);
        var days = await query.GetDailyAsync(from, to);
        Assert.Equal(to.DayNumber - from.DayNumber + 1, days.Count);
        Assert.Equal(1, days[0].KeyPressCount);
        Assert.Equal(1, days[^1].KeyPressCount);

        var (customFrom, customTo) = TrendRangeResolver.Resolve(
            TrendRangeKind.Custom,
            today,
            null,
            today,
            lastMonth);
        Assert.Equal(lastMonth, customFrom);
        Assert.Equal(today, customTo);
    }

    private static InputAggregator CreateAggregator() =>
        new(new SilentCapture(), NullLogger<InputAggregator>.Instance);

    private static KeyPressedEvent Key(string name, DateTimeOffset? timestamp = null) =>
        new()
        {
            Key = new KeyCode(name),
            Timestamp = timestamp ?? DateTimeOffset.Now
        };

    private static DateTimeOffset At(DateOnly date, int hour, int minute)
    {
        var local = date.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Local);
        return new DateTimeOffset(local);
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
}
