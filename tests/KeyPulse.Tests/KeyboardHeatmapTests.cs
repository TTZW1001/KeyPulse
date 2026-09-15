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

public class KeyboardHeatmapTests : IDisposable
{
    private readonly string _root;
    private readonly StatisticsRepository _repository;

    public KeyboardHeatmapTests()
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
    public void Layout_Is104Keys_AndContainsCoreCodes()
    {
        Assert.Equal(104, KeyboardLayoutDefinition.Keys.Count);
        Assert.Equal(104, KeyboardLayoutDefinition.KeyCount);
        var codes = KeyboardLayoutDefinition.Keys.Select(key => key.KeyCode).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("A", codes);
        Assert.Contains("Space", codes);
        Assert.Contains("Enter", codes);
        Assert.Contains("LeftShift", codes);
        Assert.Contains("LeftCtrl", codes);
        Assert.Contains("Escape", codes);
        Assert.Contains("F1", codes);
        Assert.Contains("NumPad0", codes);
        Assert.Contains("ArrowUp", codes);
    }

    [Fact]
    public void HeatmapScale_ZeroMax_IsZero()
    {
        Assert.Equal(0, HeatmapScale.Normalize(0, 0));
        Assert.Equal(0, HeatmapScale.Normalize(10, 0));
        Assert.Equal(0, HeatmapScale.Normalize(0, 100));
        Assert.Equal(1, HeatmapScale.Normalize(9, 9));
        Assert.True(HeatmapScale.Normalize(1, 100) < HeatmapScale.Normalize(50, 100));
    }

    [Fact]
    public async Task Merge_DbPlusUnflushed_InRange()
    {
        using var aggregator = CreateAggregator();
        var today = DateOnly.FromDateTime(DateTime.Now);
        for (var i = 0; i < 5; i++)
        {
            aggregator.Record(Key("A"));
        }

        await _repository.FlushAsync(aggregator.SwapForFlush());
        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));

        var query = new KeyboardQueryService(_repository, aggregator);
        var counts = await query.GetKeyCountsAsync(today, today);
        Assert.Equal(8, counts["A"]);
    }

    [Fact]
    public async Task TodayFilter_ExcludesYesterday()
    {
        using var aggregator = CreateAggregator();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);
        aggregator.Record(Key("A", At(yesterday, 18, 0)));
        aggregator.Record(Key("A", At(yesterday, 18, 1)));
        aggregator.Record(Key("A", At(today, 10, 0)));
        await _repository.FlushAsync(aggregator.SwapForFlush());
        aggregator.Record(Key("A", At(today, 10, 1)));

        var query = new KeyboardQueryService(_repository, aggregator);
        var todayCounts = await query.GetKeyCountsAsync(today, today);
        var weekCounts = await query.GetKeyCountsAsync(today.AddDays(-6), today);

        Assert.Equal(2, todayCounts["A"]);
        Assert.Equal(4, weekCounts["A"]);
        Assert.DoesNotContain("Space", todayCounts.Keys);
    }

    [Fact]
    public async Task AllZero_DoesNotThrow()
    {
        using var aggregator = CreateAggregator();
        var query = new KeyboardQueryService(_repository, aggregator);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var counts = await query.GetKeyCountsAsync(today, today);
        Assert.Empty(counts);
        Assert.Equal(0, HeatmapScale.Normalize(0, counts.Values.DefaultIfEmpty(0).Max()));
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
