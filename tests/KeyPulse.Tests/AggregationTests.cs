using KeyPulse.Core.Events;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Models;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure;
using KeyPulse.Infrastructure.Aggregation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KeyPulse.Tests;

public class AggregationTests
{
    [Fact]
    public void Pause_DoesNotAccumulate_ResumeContinues()
    {
        using var aggregator = Create();
        aggregator.Record(Key("A"));
        aggregator.SetState(TrackingState.Paused);
        aggregator.Record(Key("A"));
        aggregator.SetState(TrackingState.Running);
        aggregator.Record(Key("A"));

        var snap = aggregator.CaptureSnapshot();
        Assert.Equal(2, snap.KeyCounts["A"]);
        Assert.Equal(TrackingState.Running, snap.State);
    }

    [Fact]
    public void Swap_ReturnsIncrement_AndDoesNotMutateOldBatch()
    {
        using var aggregator = Create();
        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));

        var first = aggregator.SwapForFlush();
        Assert.Equal(3, first.KeyCountsByDate.Single().Value["A"]);

        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));

        Assert.Equal(3, first.KeyCountsByDate.Single().Value["A"]);
        Assert.Equal(2, aggregator.CaptureSnapshot().KeyCounts["A"]);

        var second = aggregator.SwapForFlush();
        Assert.Equal(2, second.KeyCountsByDate.Single().Value["A"]);
        Assert.Equal(3, first.KeyCountsByDate.Single().Value["A"]);
        Assert.Empty(aggregator.CaptureSnapshot().KeyCounts);
    }

    [Fact]
    public void MidnightEvents_GoToDifferentDateAndHourBuckets()
    {
        using var aggregator = Create();
        var before = Local(2026, 9, 15, 23, 59);
        var after = Local(2026, 9, 16, 0, 1);

        aggregator.Record(Key("A", before));
        aggregator.Record(Key("A", after));

        var snap = aggregator.CaptureSnapshot();
        var late = new HourBucket(new DateOnly(2026, 9, 15), 23);
        var early = new HourBucket(new DateOnly(2026, 9, 16), 0);

        Assert.Equal(1, snap.HourlyCounts[late].KeyPressCount);
        Assert.Equal(1, snap.HourlyCounts[early].KeyPressCount);

        var batch = aggregator.SwapForFlush();
        Assert.Equal(1, batch.KeyCountsByDate[new DateOnly(2026, 9, 15)]["A"]);
        Assert.Equal(1, batch.KeyCountsByDate[new DateOnly(2026, 9, 16)]["A"]);
    }

    [Fact]
    public void WheelDirection_IsSeparated()
    {
        using var aggregator = Create();
        aggregator.Record(new MouseWheelEvent { Timestamp = DateTimeOffset.Now, Delta = 120, Horizontal = false });
        aggregator.Record(new MouseWheelEvent { Timestamp = DateTimeOffset.Now, Delta = -120, Horizontal = false });
        aggregator.Record(new MouseWheelEvent { Timestamp = DateTimeOffset.Now, Delta = 120, Horizontal = true });

        var mouse = aggregator.CaptureSnapshot().Mouse;
        Assert.Equal(1, mouse.WheelUp);
        Assert.Equal(1, mouse.WheelDown);
        Assert.Equal(1, mouse.WheelRight);
        Assert.Equal(0, mouse.WheelLeft);
    }

    [Fact]
    public void ConcurrentRecord_DoesNotLoseCounts()
    {
        using var aggregator = Create();
        const int threads = 8;
        const int perThread = 500;
        Parallel.For(0, threads, _ =>
        {
            for (var i = 0; i < perThread; i++)
            {
                aggregator.Record(Key("A"));
            }
        });

        var snap = aggregator.CaptureSnapshot();
        Assert.Equal(threads * perThread, snap.KeyCounts["A"]);
    }

    [Fact]
    public void Merge_RestoresSwappedIncrementIntoActive()
    {
        using var aggregator = Create();
        aggregator.Record(Key("A"));
        aggregator.Record(Key("A"));
        var batch = aggregator.SwapForFlush();
        Assert.Empty(aggregator.CaptureSnapshot().KeyCounts);

        aggregator.Record(Key("B"));
        aggregator.Merge(batch);

        var snap = aggregator.CaptureSnapshot();
        Assert.Equal(2, snap.KeyCounts["A"]);
        Assert.Equal(1, snap.KeyCounts["B"]);
    }

    [Fact]
    public void AppCounts_StayEmpty()
    {
        using var aggregator = Create();
        aggregator.Record(Key("A"));
        Assert.Empty(aggregator.CaptureSnapshot().AppCounts);
        Assert.Empty(aggregator.SwapForFlush().AppCounts);
    }

    [Fact]
    public void CaptureEvent_IsAggregated_WhenNotPaused()
    {
        var capture = new FakeInputCapture();
        using var aggregator = new InputAggregator(capture, NullLogger<InputAggregator>.Instance);
        capture.Raise(Key("Space"));
        Assert.Equal(1, aggregator.CaptureSnapshot().KeyCounts["Space"]);
    }

    [Fact]
    public void ServiceProvider_ResolvesAggregator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyPulseInfrastructure();
        using var provider = services.BuildServiceProvider();
        var aggregator = provider.GetRequiredService<IStatisticsAggregator>();
        var reader = provider.GetRequiredService<IStatisticsReader>();
        Assert.Same(aggregator, reader);
    }

    private static InputAggregator Create() =>
        new(new FakeInputCapture(), NullLogger<InputAggregator>.Instance);

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

    private sealed class FakeInputCapture : IInputCapture
    {
        public event EventHandler<InputEvent>? InputReceived;

        public bool IsListening => true;

        public string? Error => null;

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public void Raise(InputEvent inputEvent) => InputReceived?.Invoke(this, inputEvent);
    }
}
