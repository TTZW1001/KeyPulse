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

public class MouseQueryTests : IDisposable
{
    private readonly string _root;
    private readonly StatisticsRepository _repository;

    public MouseQueryTests()
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
    public async Task Merge_DbLeftPlusUnflushedLeft()
    {
        using var aggregator = CreateAggregator();
        var today = DateOnly.FromDateTime(DateTime.Now);
        for (var i = 0; i < 10; i++)
        {
            aggregator.Record(Click(MouseButton.Left));
        }

        await _repository.FlushAsync(aggregator.SwapForFlush());
        for (var i = 0; i < 4; i++)
        {
            aggregator.Record(Click(MouseButton.Left));
        }

        var query = new MouseQueryService(_repository, aggregator);
        var totals = await query.GetMouseTotalsAsync(today, today);
        Assert.Equal(14, totals.Left);
    }

    [Fact]
    public async Task TodayFilter_ExcludesYesterday()
    {
        using var aggregator = CreateAggregator();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);
        aggregator.Record(Click(MouseButton.Left, At(yesterday, 18, 0)));
        aggregator.Record(Click(MouseButton.Left, At(yesterday, 18, 1)));
        aggregator.Record(Click(MouseButton.Right, At(today, 10, 0)));
        await _repository.FlushAsync(aggregator.SwapForFlush());
        aggregator.Record(Click(MouseButton.Right, At(today, 10, 1)));

        var query = new MouseQueryService(_repository, aggregator);
        var todayTotals = await query.GetMouseTotalsAsync(today, today);
        var weekTotals = await query.GetMouseTotalsAsync(today.AddDays(-6), today);

        Assert.Equal(0, todayTotals.Left);
        Assert.Equal(2, todayTotals.Right);
        Assert.Equal(2, weekTotals.Left);
        Assert.Equal(2, weekTotals.Right);
    }

    [Fact]
    public async Task Last7Days_LengthIs7()
    {
        using var aggregator = CreateAggregator();
        var today = DateOnly.FromDateTime(DateTime.Now);
        aggregator.Record(Click(MouseButton.Left));
        var query = new MouseQueryService(_repository, aggregator);
        var days = await query.GetLast7DaysAsync(today);

        Assert.Equal(7, days.Count);
        Assert.Equal(today.AddDays(-6), days[0].Date);
        Assert.Equal(today, days[6].Date);
        Assert.Equal(1, days[6].ClickCount);
        Assert.All(days.Take(6), point => Assert.Equal(0, point.ClickCount));
    }

    [Fact]
    public async Task AllZero_DoesNotThrow_ShareIsNotNaN()
    {
        using var aggregator = CreateAggregator();
        var query = new MouseQueryService(_repository, aggregator);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var totals = await query.GetMouseTotalsAsync(today, today);
        var days = await query.GetLast7DaysAsync(today);

        Assert.Equal(0, totals.Left);
        Assert.Equal(7, days.Count);
        var (left, right, middle) = ClickShare.Percents(totals.Left, totals.Right, totals.Middle);
        Assert.Equal(0, left);
        Assert.Equal(0, right);
        Assert.Equal(0, middle);
        Assert.False(double.IsNaN(left));
        Assert.False(double.IsNaN(right));
        Assert.False(double.IsNaN(middle));
    }

    [Fact]
    public void ClickShare_UsesPrimaryButtonsOnly()
    {
        var (left, right, middle) = ClickShare.Percents(50, 30, 20);
        Assert.Equal(50, left);
        Assert.Equal(30, right);
        Assert.Equal(20, middle);
    }

    private static InputAggregator CreateAggregator() =>
        new(new SilentCapture(), NullLogger<InputAggregator>.Instance);

    private static MouseButtonEvent Click(MouseButton button, DateTimeOffset? timestamp = null) =>
        new()
        {
            Button = button,
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
