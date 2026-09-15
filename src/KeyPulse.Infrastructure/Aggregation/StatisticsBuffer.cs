using KeyPulse.Core.Events;
using KeyPulse.Core.Models;
using KeyPulse.Core.Statistics;

namespace KeyPulse.Infrastructure.Aggregation;

internal sealed class StatisticsBuffer
{
    private readonly Dictionary<(DateOnly Date, string Key), long> _keyCounts = new();
    private readonly Dictionary<DateOnly, MouseDay> _mouseByDate = new();
    private readonly Dictionary<HourBucket, HourlyDay> _hourly = new();

    public DateTimeOffset? LastInputTime { get; private set; }

    public void Add(InputEvent inputEvent)
    {
        var local = inputEvent.Timestamp.ToLocalTime();
        var date = DateOnly.FromDateTime(local.DateTime);
        var hour = local.Hour;
        var bucket = new HourBucket(date, hour);
        LastInputTime = inputEvent.Timestamp;

        switch (inputEvent)
        {
            case KeyPressedEvent key:
                AddKey(date, key.Key.Name);
                Hour(bucket).KeyPressCount++;
                break;
            case MouseButtonEvent button:
                AddMouseButton(date, button.Button);
                Hour(bucket).MouseClickCount++;
                break;
            case MouseWheelEvent wheel:
                AddWheel(date, wheel);
                Hour(bucket).WheelEventCount++;
                break;
            case MouseMoveEvent move:
                var distance = Math.Sqrt(
                    ((double)move.DeltaX * move.DeltaX) + ((double)move.DeltaY * move.DeltaY));
                Mouse(date).DistancePixels += distance;
                Hour(bucket).MouseDistancePixels += distance;
                break;
        }
    }

    public void Clear()
    {
        _keyCounts.Clear();
        _mouseByDate.Clear();
        _hourly.Clear();
        LastInputTime = null;
    }

    public StatisticsSnapshot ToSnapshot(TrackingState state)
    {
        var keys = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var pair in _keyCounts)
        {
            keys[pair.Key.Key] = keys.GetValueOrDefault(pair.Key.Key) + pair.Value;
        }

        var mouse = MouseTotals.Zero;
        foreach (var day in _mouseByDate.Values)
        {
            mouse = mouse.Add(day.ToTotals());
        }

        return new StatisticsSnapshot(
            state,
            keys,
            mouse,
            CopyHourly(),
            new Dictionary<string, long>(StringComparer.Ordinal),
            LastInputTime);
    }

    public StatisticsBatch ToBatch()
    {
        var keysByDate = new Dictionary<DateOnly, IReadOnlyDictionary<string, long>>();
        foreach (var pair in _keyCounts)
        {
            if (!keysByDate.TryGetValue(pair.Key.Date, out var inner))
            {
                inner = new Dictionary<string, long>(StringComparer.Ordinal);
                keysByDate[pair.Key.Date] = inner;
            }

            ((Dictionary<string, long>)inner)[pair.Key.Key] = pair.Value;
        }

        var mouseByDate = new Dictionary<DateOnly, MouseTotals>();
        foreach (var pair in _mouseByDate)
        {
            mouseByDate[pair.Key] = pair.Value.ToTotals();
        }

        return new StatisticsBatch(
            keysByDate,
            mouseByDate,
            CopyHourly(),
            new Dictionary<string, long>(StringComparer.Ordinal),
            LastInputTime);
    }

    private void AddKey(DateOnly date, string key)
    {
        var mapKey = (date, key);
        _keyCounts[mapKey] = _keyCounts.GetValueOrDefault(mapKey) + 1;
    }

    private void AddMouseButton(DateOnly date, MouseButton button)
    {
        var day = Mouse(date);
        switch (button)
        {
            case MouseButton.Left: day.Left++; break;
            case MouseButton.Right: day.Right++; break;
            case MouseButton.Middle: day.Middle++; break;
            case MouseButton.XButton1: day.XButton1++; break;
            case MouseButton.XButton2: day.XButton2++; break;
        }
    }

    private void AddWheel(DateOnly date, MouseWheelEvent wheel)
    {
        var day = Mouse(date);
        if (wheel.Horizontal)
        {
            if (wheel.Delta > 0) day.WheelRight++;
            else if (wheel.Delta < 0) day.WheelLeft++;
        }
        else if (wheel.Delta > 0)
        {
            day.WheelUp++;
        }
        else if (wheel.Delta < 0)
        {
            day.WheelDown++;
        }
    }

    private MouseDay Mouse(DateOnly date)
    {
        if (!_mouseByDate.TryGetValue(date, out var day))
        {
            day = new MouseDay();
            _mouseByDate[date] = day;
        }

        return day;
    }

    private HourlyDay Hour(HourBucket bucket)
    {
        if (!_hourly.TryGetValue(bucket, out var hour))
        {
            hour = new HourlyDay();
            _hourly[bucket] = hour;
        }

        return hour;
    }

    private Dictionary<HourBucket, HourlyActivity> CopyHourly()
    {
        var copy = new Dictionary<HourBucket, HourlyActivity>();
        foreach (var pair in _hourly)
        {
            copy[pair.Key] = pair.Value.ToActivity();
        }

        return copy;
    }

    private sealed class MouseDay
    {
        public long Left;
        public long Right;
        public long Middle;
        public long XButton1;
        public long XButton2;
        public long WheelUp;
        public long WheelDown;
        public long WheelLeft;
        public long WheelRight;
        public double DistancePixels;

        public MouseTotals ToTotals() => new(
            Left, Right, Middle, XButton1, XButton2,
            WheelUp, WheelDown, WheelLeft, WheelRight,
            DistancePixels);
    }

    private sealed class HourlyDay
    {
        public long KeyPressCount;
        public long MouseClickCount;
        public long WheelEventCount;
        public double MouseDistancePixels;

        public HourlyActivity ToActivity() => new(
            KeyPressCount, MouseClickCount, WheelEventCount, MouseDistancePixels);
    }
}
