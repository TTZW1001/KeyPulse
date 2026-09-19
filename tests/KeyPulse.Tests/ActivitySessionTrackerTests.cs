using KeyPulse.Infrastructure.Aggregation;
using Xunit;

namespace KeyPulse.Tests;

public sealed class ActivitySessionTrackerTests
{
    [Fact]
    public void Advance_CountsConfiguredAfkGrace_ThenStops()
    {
        var tracker = new ActivitySessionTracker();
        var start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.Zero);
        tracker.ObserveInput(start, TimeSpan.FromMinutes(5), true, false, false);

        var active = tracker.Advance(start.AddMinutes(4), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5));
        var away = tracker.Advance(start.AddMinutes(6), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5));

        Assert.True(active.IsActive);
        Assert.Equal(1, active.Seconds);
        Assert.False(away.IsActive);
        Assert.Equal(1, away.Session!.EffectiveSeconds);
    }

    [Fact]
    public void ObserveInput_AfterFifteenMinutes_StartsNewSession()
    {
        var tracker = new ActivitySessionTracker();
        var start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.Zero);
        var first = tracker.ObserveInput(start, TimeSpan.FromMinutes(5), true, false, false).Single();

        var changed = tracker.ObserveInput(start.AddMinutes(15), TimeSpan.FromMinutes(5), false, true, false);

        Assert.Equal(2, changed.Count);
        Assert.Equal(first.SessionId, changed[0].SessionId);
        Assert.NotEqual(first.SessionId, changed[1].SessionId);
        Assert.Equal(start.AddMinutes(5), changed[0].EndedAt);
        Assert.Equal(1, changed[1].MouseClickCount);
    }
}
