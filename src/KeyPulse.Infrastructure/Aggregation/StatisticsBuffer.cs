using KeyPulse.Core.Events;
using KeyPulse.Core.Models;
using KeyPulse.Core.Statistics;

namespace KeyPulse.Infrastructure.Aggregation;

internal sealed class StatisticsBuffer
{
    private readonly Dictionary<(DateOnly Date, string Key), long> _keyCounts = new();
    private readonly Dictionary<(DateOnly Date, string Shortcut), long> _shortcutCounts = new();
    private readonly Dictionary<DateOnly, MouseDay> _mouseByDate = new();
    private readonly Dictionary<HourBucket, HourlyDay> _hourly = new();
    private readonly Dictionary<DateOnly, Dictionary<string, AppDay>> _apps = new();
    private readonly Dictionary<string, DisplayLayout> _displayLayouts = new(StringComparer.Ordinal);
    private readonly Dictionary<ClickPointKey, long> _clickPoints = new();
    private readonly Dictionary<PointerDensityKey, DensityDay> _pointerDensity = new();
    private readonly Dictionary<OccupancyTileKey, OccupancyTile> _occupancyTiles = new();

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
                if (button.Position is { } clickPosition)
                {
                    AddClick(local, clickPosition, button.Button);
                }
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
                Mouse(date).CursorDistancePixels += move.CursorDistancePixels;
                Mouse(date).EstimatedDistanceMeters += move.EstimatedDistanceMeters;
                Hour(bucket).MouseDistancePixels += distance;
                Hour(bucket).CursorDistancePixels += move.CursorDistancePixels;
                Hour(bucket).EstimatedDistanceMeters += move.EstimatedDistanceMeters;
                if (app is not null)
                {
                    App(date, app).MouseDistancePixels += distance;
                    App(date, app).CursorDistancePixels += move.CursorDistancePixels;
                    App(date, app).EstimatedDistanceMeters += move.EstimatedDistanceMeters;
                }

                if (move.Position is { } position && move.PreviousTrajectoryPosition is { } previous)
                {
                    AddTrajectory(date, previous, position);
                }

                break;
        }
    }

    public void AddShortcut(DateTimeOffset timestamp, string shortcut)
    {
        var date = DateOnly.FromDateTime(timestamp.ToLocalTime().DateTime);
        var mapKey = (date, shortcut);
        _shortcutCounts[mapKey] = _shortcutCounts.GetValueOrDefault(mapKey) + 1;
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
        _shortcutCounts.Clear();
        _mouseByDate.Clear();
        _hourly.Clear();
        _apps.Clear();
        _displayLayouts.Clear();
        _clickPoints.Clear();
        _pointerDensity.Clear();
        _occupancyTiles.Clear();
        LastInputTime = null;
    }

    public void ClearPositionData()
    {
        _displayLayouts.Clear();
        _clickPoints.Clear();
        _pointerDensity.Clear();
        _occupancyTiles.Clear();
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
            CopyShortcutCounts(),
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
            CopyShortcutCountsByDate(),
            mouseByDate,
            CopyHourly(),
            CopyAppCounts(),
            CopyAppStats(),
            LastInputTime,
            CopyPointerStatistics());
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

        foreach (var pair in batch.ShortcutCountsByDate)
        {
            foreach (var shortcut in pair.Value)
            {
                var mapKey = (pair.Key, shortcut.Key);
                _shortcutCounts[mapKey] = _shortcutCounts.GetValueOrDefault(mapKey) + shortcut.Value;
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

        if (batch.Pointer is { } pointer)
        {
            MergePointer(pointer);
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

    private Dictionary<string, long> CopyShortcutCounts()
    {
        var copy = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var pair in _shortcutCounts)
        {
            copy[pair.Key.Shortcut] = copy.GetValueOrDefault(pair.Key.Shortcut) + pair.Value;
        }

        return copy;
    }

    private Dictionary<DateOnly, IReadOnlyDictionary<string, long>> CopyShortcutCountsByDate()
    {
        var copy = new Dictionary<DateOnly, IReadOnlyDictionary<string, long>>();
        foreach (var pair in _shortcutCounts)
        {
            if (!copy.TryGetValue(pair.Key.Date, out var values))
            {
                values = new Dictionary<string, long>(StringComparer.Ordinal);
                copy[pair.Key.Date] = values;
            }

            ((Dictionary<string, long>)values)[pair.Key.Shortcut] = pair.Value;
        }

        return copy;
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

    private void AddClick(DateTimeOffset local, PointerPosition position, MouseButton button)
    {
        RememberLayout(position.Layout);
        var x = position.MonitorX;
        var y = position.MonitorY;
        if (x < 0 || y < 0 || x >= position.Monitor.Width || y >= position.Monitor.Height) return;
        var key = new ClickPointKey(
            DateOnly.FromDateTime(local.DateTime), local.Hour,
            position.Layout.Signature, position.Monitor.Id, x, y, button.ToString());
        _clickPoints[key] = _clickPoints.GetValueOrDefault(key) + 1;
    }

    private void AddTrajectory(DateOnly date, PointerPosition from, PointerPosition to)
    {
        RememberLayout(to.Layout);
        if (!string.Equals(from.Layout.Signature, to.Layout.Signature, StringComparison.Ordinal) ||
            !string.Equals(from.Monitor.Id, to.Monitor.Id, StringComparison.Ordinal))
        {
            MarkPoint(date, to, to.MonitorX, to.MonitorY);
            return;
        }

        var x0 = from.MonitorX;
        var y0 = from.MonitorY;
        var x1 = to.MonitorX;
        var y1 = to.MonitorY;
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;
        var remaining = Math.Max(dx, -dy) + 1;
        var limit = Math.Max(to.Monitor.Width, to.Monitor.Height) * 2;
        if (remaining > limit) return;

        while (remaining-- > 0)
        {
            MarkPoint(date, to, x0, y0);
            if (x0 == x1 && y0 == y1) break;
            var twice = 2 * error;
            if (twice >= dy) { error += dy; x0 += sx; }
            if (twice <= dx) { error += dx; y0 += sy; }
        }
    }

    private void MarkPoint(DateOnly date, PointerPosition position, int x, int y)
    {
        if (x < 0 || y < 0 || x >= position.Monitor.Width || y >= position.Monitor.Height) return;

        const int gridWidth = 256;
        var gridHeight = Math.Max(1, (int)Math.Round(gridWidth * position.Monitor.Height / (double)position.Monitor.Width));
        var densityKey = new PointerDensityKey(date, position.Layout.Signature, position.Monitor.Id);
        if (!_pointerDensity.TryGetValue(densityKey, out var density))
        {
            density = new DensityDay(gridWidth, gridHeight);
            _pointerDensity[densityKey] = density;
        }

        var cellX = Math.Min(gridWidth - 1, x * gridWidth / position.Monitor.Width);
        var cellY = Math.Min(gridHeight - 1, y * gridHeight / position.Monitor.Height);
        density.Increment(cellX, cellY);

        const int tileSize = 256;
        var tileX = x / tileSize;
        var tileY = y / tileSize;
        var tileWidth = Math.Min(tileSize, position.Monitor.Width - (tileX * tileSize));
        var tileHeight = Math.Min(tileSize, position.Monitor.Height - (tileY * tileSize));
        var tileKey = new OccupancyTileKey(position.Layout.Signature, position.Monitor.Id, tileX, tileY);
        if (!_occupancyTiles.TryGetValue(tileKey, out var tile))
        {
            tile = new OccupancyTile(tileWidth, tileHeight);
            _occupancyTiles[tileKey] = tile;
        }

        tile.Set(x % tileSize, y % tileSize);
    }

    private void RememberLayout(DisplayLayout layout) => _displayLayouts[layout.Signature] = layout;

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

    private PointerStatistics CopyPointerStatistics()
    {
        var densities = _pointerDensity.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToData());
        var tiles = _occupancyTiles.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToData());
        return new PointerStatistics(
            new Dictionary<string, DisplayLayout>(_displayLayouts, StringComparer.Ordinal),
            new Dictionary<ClickPointKey, long>(_clickPoints), densities, tiles);
    }

    private void MergePointer(PointerStatistics pointer)
    {
        foreach (var layout in pointer.Layouts) _displayLayouts[layout.Key] = layout.Value;
        foreach (var click in pointer.Clicks)
            _clickPoints[click.Key] = _clickPoints.GetValueOrDefault(click.Key) + click.Value;
        foreach (var density in pointer.Densities)
        {
            if (!_pointerDensity.TryGetValue(density.Key, out var target))
            {
                target = new DensityDay(density.Value.Width, density.Value.Height);
                _pointerDensity[density.Key] = target;
            }
            target.Add(density.Value);
        }
        foreach (var tile in pointer.OccupancyTiles)
        {
            if (!_occupancyTiles.TryGetValue(tile.Key, out var target))
            {
                target = new OccupancyTile(tile.Value.Width, tile.Value.Height);
                _occupancyTiles[tile.Key] = target;
            }
            target.Or(tile.Value.Bits);
        }
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
        public double CursorDistancePixels;
        public double EstimatedDistanceMeters;

        public MouseTotals ToTotals() => new(
            Left, Right, Middle, XButton1, XButton2,
            WheelUp, WheelDown, WheelLeft, WheelRight,
            DistancePixels, CursorDistancePixels, EstimatedDistanceMeters);

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
            CursorDistancePixels += totals.CursorDistancePixels;
            EstimatedDistanceMeters += totals.EstimatedDistanceMeters;
        }
    }

    private sealed class HourlyDay
    {
        public long KeyPressCount;
        public long MouseClickCount;
        public long WheelEventCount;
        public double MouseDistancePixels;
        public double CursorDistancePixels;
        public double EstimatedDistanceMeters;

        public HourlyActivity ToActivity() => new(
            KeyPressCount, MouseClickCount, WheelEventCount, MouseDistancePixels,
            CursorDistancePixels, EstimatedDistanceMeters);

        public void Add(in HourlyActivity activity)
        {
            KeyPressCount += activity.KeyPressCount;
            MouseClickCount += activity.MouseClickCount;
            WheelEventCount += activity.WheelEventCount;
            MouseDistancePixels += activity.MouseDistancePixels;
            CursorDistancePixels += activity.CursorDistancePixels;
            EstimatedDistanceMeters += activity.EstimatedDistanceMeters;
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
        public double CursorDistancePixels;
        public double EstimatedDistanceMeters;
        public long ActiveSeconds;

        public long ActivityCount => KeyPressCount + MouseClickCount + WheelEventCount;

        public AppDayTotals ToTotals() => new(
            KeyPressCount, MouseClickCount, WheelEventCount,
            MouseDistancePixels, ActiveSeconds, DisplayName,
            CursorDistancePixels, EstimatedDistanceMeters);

        public void Add(in AppDayTotals totals)
        {
            KeyPressCount += totals.KeyPressCount;
            MouseClickCount += totals.MouseClickCount;
            WheelEventCount += totals.WheelEventCount;
            MouseDistancePixels += totals.MouseDistancePixels;
            CursorDistancePixels += totals.CursorDistancePixels;
            EstimatedDistanceMeters += totals.EstimatedDistanceMeters;
            ActiveSeconds += totals.ActiveSeconds;
            if (string.IsNullOrWhiteSpace(DisplayName) &&
                !string.IsNullOrWhiteSpace(totals.DisplayName))
            {
                DisplayName = totals.DisplayName;
            }
        }
    }

    private sealed class DensityDay
    {
        private readonly uint[] _cells;
        public DensityDay(int width, int height)
        {
            Width = width;
            Height = height;
            _cells = new uint[width * height];
        }
        public int Width { get; }
        public int Height { get; }
        public long SampleCount { get; private set; }
        public void Increment(int x, int y)
        {
            var index = (y * Width) + x;
            if (_cells[index] < uint.MaxValue) _cells[index]++;
            SampleCount++;
        }
        public void Add(PointerDensityData data)
        {
            if (data.Width != Width || data.Height != Height) return;
            for (var i = 0; i < _cells.Length; i++)
                _cells[i] = (uint)Math.Min(uint.MaxValue, (ulong)_cells[i] + data.Cells[i]);
            SampleCount += data.SampleCount;
        }
        public PointerDensityData ToData() => new(Width, Height, SampleCount, (uint[])_cells.Clone());
    }

    private sealed class OccupancyTile
    {
        private readonly byte[] _bits;
        public OccupancyTile(int width, int height)
        {
            Width = width;
            Height = height;
            _bits = new byte[((width * height) + 7) / 8];
        }
        public int Width { get; }
        public int Height { get; }
        public void Set(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            var index = (y * Width) + x;
            _bits[index >> 3] |= (byte)(1 << (index & 7));
        }
        public void Or(byte[] other)
        {
            for (var i = 0; i < Math.Min(_bits.Length, other.Length); i++) _bits[i] |= other[i];
        }
        public OccupancyTileData ToData() => new(Width, Height, (byte[])_bits.Clone());
    }
}
