using KeyPulse.Core.Events;
using KeyPulse.Core.Models;
using KeyPulse.Core.Statistics;

namespace KeyPulse.Infrastructure.Aggregation;

internal sealed class StatisticsBuffer
{
    private readonly Dictionary<(DateOnly Date, string Key), long> _keyCounts = new();
    private readonly Dictionary<DateOnly, MouseDay> _mouseByDate = new();
    private readonly Dictionary<HourBucket, HourlyDay> _hourly = new();
    private readonly Dictionary<DateOnly, Dictionary<string, AppDay>> _apps = new();

    public DateTimeOffset? LastInputTime { get; private set; }

    public void Add(InputEvent inputEvent, ForegroundApp? app)
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
                if (app is not null)
                {
                    App(date, app).KeyPressCount++;
                }

                break;
            case MouseButtonEvent button:
                AddMouseButton(date, button.Button);
                Hour(bucket).MouseClickCount++;
                if (app is not null)
                {
                    App(date, app).MouseClickCount++;
                }

                break;
            case MouseWheelEvent wheel:
                AddWheel(date, wheel);
                Hour(bucket).WheelEventCount++;
                if (app is not null)
                {
                    App(date, app).WheelEventCount++;
                }

                break;
            case MouseMoveEvent move:
                var distance = Math.Sqrt(
                    ((double)move.DeltaX * move.DeltaX) + ((double)move.DeltaY * move.DeltaY));
                Mouse(date).DistancePixels += distance;
                Hour(bucket).MouseDistancePixels += distance;
                if (app is not null)
                {
                    App(date, app).MouseDistancePixels += distance;
                }

                break;
        }
    }

    public void AddActive(ForegroundApp app, TimeSpan elapsed, DateTimeOffset timestamp)
    {
        if (elapsed < TimeSpan.FromMilliseconds(500) || elapsed > TimeSpan.FromSeconds(5))
        {
            return;
        }

        var seconds = Math.Max(1, (long)Math.Round(elapsed.TotalSeconds));
        var date = DateOnly.FromDateTime(timestamp.ToLocalTime().DateTime);
        App(date, app).ActiveSeconds += seconds;
    }

    public void Clear()
    {
        _keyCounts.Clear();
        _mouseByDate.Clear();
        _hourly.Clear();
        _apps.Clear();
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
            CopyAppCounts(),
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
            CopyAppCounts(),
            CopyAppStats(),
            LastInputTime);
    }

    public void Merge(StatisticsBatch batch)
    {
        foreach (var pair in batch.KeyCountsByDate)
        {
            foreach (var key in pair.Value)
            {
                var mapKey = (pair.Key, key.Key);
                _keyCounts[mapKey] = _keyCounts.GetValueOrDefault(mapKey) + key.Value;
            }
        }

        foreach (var pair in batch.MouseByDate)
        {
            Mouse(pair.Key).Add(pair.Value);
        }

        foreach (var pair in batch.HourlyCounts)
        {
            Hour(pair.Key).Add(pair.Value);
        }

        foreach (var day in batch.AppStatsByDate)
        {
            foreach (var app in day.Value)
            {
                App(day.Key, app.Key, app.Value.DisplayName).Add(app.Value);
            }
        }

        if (batch.LastInputTime is { } time &&
            (LastInputTime is null || time > LastInputTime))
        {
            LastInputTime = time;
        }
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

    private AppDay App(DateOnly date, ForegroundApp app) =>
        App(date, app.ProcessName, app.DisplayName);

    private AppDay App(DateOnly date, string processName, string? displayName)
    {
        if (!_apps.TryGetValue(date, out var byName))
        {
            byName = new Dictionary<string, AppDay>(StringComparer.OrdinalIgnoreCase);
            _apps[date] = byName;
        }

        if (!byName.TryGetValue(processName, out var day))
        {
            day = new AppDay { ProcessName = processName, DisplayName = displayName };
            byName[processName] = day;
        }
        else if (string.IsNullOrWhiteSpace(day.DisplayName) &&
                 !string.IsNullOrWhiteSpace(displayName))
        {
            day.DisplayName = displayName;
        }

        return day;
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

    private Dictionary<string, long> CopyAppCounts()
    {
        var counts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var day in _apps.Values)
        {
            foreach (var app in day.Values)
            {
                counts[app.ProcessName] = counts.GetValueOrDefault(app.ProcessName) + app.ActivityCount;
            }
        }

        return counts;
    }

    private Dictionary<DateOnly, IReadOnlyDictionary<string, AppDayTotals>> CopyAppStats()
    {
        var copy = new Dictionary<DateOnly, IReadOnlyDictionary<string, AppDayTotals>>();
        foreach (var day in _apps)
        {
            var inner = new Dictionary<string, AppDayTotals>(StringComparer.OrdinalIgnoreCase);
            foreach (var app in day.Value)
            {
                inner[app.Value.ProcessName] = app.Value.ToTotals();
            }

            copy[day.Key] = inner;
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

        public void Add(in MouseTotals totals)
        {
            Left += totals.Left;
            Right += totals.Right;
            Middle += totals.Middle;
            XButton1 += totals.XButton1;
            XButton2 += totals.XButton2;
            WheelUp += totals.WheelUp;
            WheelDown += totals.WheelDown;
            WheelLeft += totals.WheelLeft;
            WheelRight += totals.WheelRight;
            DistancePixels += totals.DistancePixels;
        }
    }

    private sealed class HourlyDay
    {
        public long KeyPressCount;
        public long MouseClickCount;
        public long WheelEventCount;
        public double MouseDistancePixels;

        public HourlyActivity ToActivity() => new(
            KeyPressCount, MouseClickCount, WheelEventCount, MouseDistancePixels);

        public void Add(in HourlyActivity activity)
        {
            KeyPressCount += activity.KeyPressCount;
            MouseClickCount += activity.MouseClickCount;
            WheelEventCount += activity.WheelEventCount;
            MouseDistancePixels += activity.MouseDistancePixels;
        }
    }

    private sealed class AppDay
    {
        public string ProcessName = string.Empty;
        public string? DisplayName;
        public long KeyPressCount;
        public long MouseClickCount;
        public long WheelEventCount;
        public double MouseDistancePixels;
        public long ActiveSeconds;

        public long ActivityCount => KeyPressCount + MouseClickCount + WheelEventCount;

        public AppDayTotals ToTotals() => new(
            KeyPressCount, MouseClickCount, WheelEventCount,
            MouseDistancePixels, ActiveSeconds, DisplayName);

        public void Add(in AppDayTotals totals)
        {
            KeyPressCount += totals.KeyPressCount;
            MouseClickCount += totals.MouseClickCount;
            WheelEventCount += totals.WheelEventCount;
            MouseDistancePixels += totals.MouseDistancePixels;
            ActiveSeconds += totals.ActiveSeconds;
            if (string.IsNullOrWhiteSpace(DisplayName) &&
                !string.IsNullOrWhiteSpace(totals.DisplayName))
            {
                DisplayName = totals.DisplayName;
            }
        }
    }
}
