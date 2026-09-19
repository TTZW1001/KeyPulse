using KeyPulse.Core.Statistics;

namespace KeyPulse.Infrastructure.Aggregation;

internal sealed class ActivitySessionTracker
{
    private static readonly TimeSpan SessionGap = TimeSpan.FromMinutes(15);
    private MutableSession? _current;
    private DateTimeOffset? _lastInput;

    public IReadOnlyList<ActivitySession> ObserveInput(
        DateTimeOffset timestamp,
        TimeSpan afkThreshold,
        bool keyPress,
        bool mouseClick,
        bool wheel)
    {
        var changed = new List<ActivitySession>(2);
        var localDate = DateOnly.FromDateTime(timestamp.ToLocalTime().DateTime);
        if (_current is not null &&
            (_lastInput is null || timestamp - _lastInput >= SessionGap || _current.Date != localDate))
        {
            changed.Add(CloseAt(CalculateEnd(timestamp, afkThreshold))!);
        }

        _current ??= new MutableSession(Guid.NewGuid().ToString("N"), localDate, timestamp);
        _lastInput = timestamp;
        _current.EndedAt = timestamp;
        if (keyPress) _current.KeyPressCount++;
        if (mouseClick) _current.MouseClickCount++;
        if (wheel) _current.WheelEventCount++;
        changed.Add(_current.ToRecord());
        return changed;
    }

    public ActivityTick Advance(DateTimeOffset timestamp, TimeSpan elapsed, TimeSpan afkThreshold)
    {
        if (_current is null || _lastInput is null)
        {
            return ActivityTick.None;
        }

        if (timestamp - _lastInput >= SessionGap)
        {
            return new ActivityTick(false, 0, CloseAt(CalculateEnd(timestamp, afkThreshold)));
        }

        var active = timestamp - _lastInput <= afkThreshold;
        if (!active || elapsed < TimeSpan.FromMilliseconds(500) || elapsed > TimeSpan.FromSeconds(5))
        {
            return new ActivityTick(false, 0, _current.ToRecord());
        }

        var seconds = Math.Max(1, (long)Math.Round(elapsed.TotalSeconds));
        _current.EffectiveSeconds += seconds;
        _current.EndedAt = timestamp;
        return new ActivityTick(true, seconds, _current.ToRecord());
    }

    public ActivitySession? Close(DateTimeOffset timestamp, TimeSpan afkThreshold)
    {
        if (_current is null) return null;
        return CloseAt(CalculateEnd(timestamp, afkThreshold));
    }

    public void Reset()
    {
        _current = null;
        _lastInput = null;
    }

    private DateTimeOffset CalculateEnd(DateTimeOffset now, TimeSpan afkThreshold)
    {
        if (_lastInput is null) return now;
        var graceEnd = _lastInput.Value + afkThreshold;
        return graceEnd < now ? graceEnd : now;
    }

    private ActivitySession? CloseAt(DateTimeOffset timestamp)
    {
        if (_current is null) return null;
        if (timestamp > _current.EndedAt) _current.EndedAt = timestamp;
        var result = _current.ToRecord();
        _current = null;
        _lastInput = null;
        return result;
    }

    private sealed class MutableSession(string id, DateOnly date, DateTimeOffset startedAt)
    {
        public string Id { get; } = id;
        public DateOnly Date { get; } = date;
        public DateTimeOffset StartedAt { get; } = startedAt;
        public DateTimeOffset EndedAt { get; set; } = startedAt;
        public long EffectiveSeconds { get; set; }
        public long KeyPressCount { get; set; }
        public long MouseClickCount { get; set; }
        public long WheelEventCount { get; set; }

        public ActivitySession ToRecord() => new(
            Id, Date, StartedAt, EndedAt, EffectiveSeconds,
            KeyPressCount, MouseClickCount, WheelEventCount);
    }
}

internal readonly record struct ActivityTick(bool IsActive, long Seconds, ActivitySession? Session)
{
    public static ActivityTick None { get; } = new(false, 0, null);
}
