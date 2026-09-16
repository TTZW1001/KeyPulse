using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Persistence;

namespace KeyPulse.Infrastructure.Query;

public sealed class DashboardQueryService : IDashboardQuery
{
    private readonly IStatisticsRepository _repository;
    private readonly IStatisticsReader _reader;

    public DashboardQueryService(IStatisticsRepository repository, IStatisticsReader reader)
    {
        _repository = repository;
        _reader = reader;
    }

    public Task<DashboardToday> GetTodayAsync(CancellationToken cancellationToken = default) =>
        GetTodayAsync(Today(), cancellationToken);

    public async Task<DashboardToday> GetTodayAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var persistedKeys = await ReadAsync(
            () => _repository.GetKeyStatsAsync(date, date, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false);
        var persistedMouse = await ReadAsync(
            () => _repository.GetMouseStatsAsync(date, date, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false);
        var unflushed = _reader.CaptureUnflushed();

        var keys = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var row in persistedKeys)
        {
            keys[row.KeyCode] = keys.GetValueOrDefault(row.KeyCode) + row.PressCount;
        }

        if (unflushed.KeyCountsByDate.TryGetValue(date, out var snapKeys))
        {
            foreach (var pair in snapKeys)
            {
                keys[pair.Key] = keys.GetValueOrDefault(pair.Key) + pair.Value;
            }
        }

        var mouse = MouseTotals.Zero;
        foreach (var row in persistedMouse)
        {
            mouse = mouse.Add(row.Mouse);
        }

        if (unflushed.MouseByDate.TryGetValue(date, out var snapMouse))
        {
            mouse = mouse.Add(snapMouse);
        }

        string? topKey = null;
        long topCount = 0;
        foreach (var pair in keys.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal))
        {
            if (pair.Value <= 0)
            {
                break;
            }

            topKey = pair.Key;
            topCount = pair.Value;
            break;
        }

        return new DashboardToday(
            date,
            keys.Values.Sum(),
            Clicks(mouse),
            Wheel(mouse),
            mouse.CursorDistancePixels,
            topKey,
            topCount,
            _reader.State);
    }

    public Task<IReadOnlyList<DailyTrendPoint>> GetLast7DaysAsync(CancellationToken cancellationToken = default) =>
        GetLast7DaysAsync(Today(), cancellationToken);

    public async Task<IReadOnlyList<DailyTrendPoint>> GetLast7DaysAsync(
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = endDate.AddDays(-6);
        var persistedKeys = await ReadAsync(
            () => _repository.GetKeyStatsAsync(start, endDate, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false);
        var persistedMouse = await ReadAsync(
            () => _repository.GetMouseStatsAsync(start, endDate, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false);
        var unflushed = _reader.CaptureUnflushed();

        var points = new DailyTrendPoint[7];
        for (var i = 0; i < 7; i++)
        {
            var date = start.AddDays(i);
            var keys = persistedKeys.Where(row => row.Date == date).Sum(row => row.PressCount);
            if (unflushed.KeyCountsByDate.TryGetValue(date, out var snapKeys))
            {
                keys += snapKeys.Values.Sum();
            }

            var mouse = MouseTotals.Zero;
            foreach (var row in persistedMouse.Where(row => row.Date == date))
            {
                mouse = mouse.Add(row.Mouse);
            }

            if (unflushed.MouseByDate.TryGetValue(date, out var snapMouse))
            {
                mouse = mouse.Add(snapMouse);
            }

            points[i] = new DailyTrendPoint(date, keys, Clicks(mouse), Wheel(mouse));
        }

        return points;
    }

    public Task<IReadOnlyList<HourlyPoint>> GetTodayHourlyAsync(CancellationToken cancellationToken = default) =>
        GetTodayHourlyAsync(Today(), cancellationToken);

    public async Task<IReadOnlyList<HourlyPoint>> GetTodayHourlyAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var persisted = await ReadAsync(
            () => _repository.GetHourlyAsync(date, date, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false);
        var unflushed = _reader.CaptureUnflushed();

        var hours = new long[24];
        foreach (var row in persisted)
        {
            if (row.Hour is >= 0 and <= 23)
            {
                hours[row.Hour] += row.KeyPressCount + row.MouseClickCount + row.WheelEventCount;
            }
        }

        foreach (var pair in unflushed.HourlyCounts)
        {
            if (pair.Key.Date != date || pair.Key.Hour is < 0 or > 23)
            {
                continue;
            }

            hours[pair.Key.Hour] +=
                pair.Value.KeyPressCount + pair.Value.MouseClickCount + pair.Value.WheelEventCount;
        }

        var points = new HourlyPoint[24];
        for (var hour = 0; hour < 24; hour++)
        {
            points[hour] = new HourlyPoint(hour, hours[hour]);
        }

        return points;
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    private static long Clicks(in MouseTotals mouse) =>
        mouse.Left + mouse.Right + mouse.Middle + mouse.XButton1 + mouse.XButton2;

    private static long Wheel(in MouseTotals mouse) =>
        mouse.WheelUp + mouse.WheelDown + mouse.WheelLeft + mouse.WheelRight;

    private static Task<T> ReadAsync<T>(Func<T> read, CancellationToken cancellationToken) =>
        Task.Run(read, cancellationToken);
}
