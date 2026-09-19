using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Models;
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
        var persistedHours = await ReadAsync(
            () => _repository.GetHourlyAsync(date, date, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false);
        var persistedSessions = await ReadAsync(
            () => _repository.GetActivitySessionsAsync(date, date, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false);

        var keys = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var row in persistedKeys)
        {
            if (!KeyCode.IsValidStatisticName(row.KeyCode))
            {
                continue;
            }

            keys[row.KeyCode] = keys.GetValueOrDefault(row.KeyCode) + row.PressCount;
        }

        if (unflushed.KeyCountsByDate.TryGetValue(date, out var snapKeys))
        {
            foreach (var pair in snapKeys)
            {
                if (!KeyCode.IsValidStatisticName(pair.Key))
                {
                    continue;
                }

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

        var effectiveSeconds = persistedHours.Sum(row => row.ActiveSeconds);
        foreach (var pair in unflushed.HourlyCounts)
            if (pair.Key.Date == date) effectiveSeconds += pair.Value.EffectiveActiveSeconds;
        var sessions = persistedSessions.Select(item => item.SessionId).ToHashSet(StringComparer.Ordinal);
        if (unflushed.ActivitySessions is { } liveSessions)
            foreach (var session in liveSessions.Where(item => item.Date == date)) sessions.Add(session.SessionId);
        var trackingStart = await _repository.GetMetaAsync("effective_tracking_started_at", cancellationToken).ConfigureAwait(false);
        var hasEffectiveTime = DateOnly.TryParse(trackingStart, out var trackingDate) && date >= trackingDate;

        return new DashboardToday(
            date,
            keys.Values.Sum(),
            Clicks(mouse),
            Wheel(mouse),
            mouse.CursorDistancePixels,
            topKey,
            topCount,
            _reader.State,
            mouse.EstimatedDistanceMeters,
            effectiveSeconds,
            sessions.Count,
            hasEffectiveTime);
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

        var keys = new long[24];
        var clicks = new long[24];
        var wheels = new long[24];
        foreach (var row in persisted)
        {
            if (row.Hour is >= 0 and <= 23)
            {
                keys[row.Hour] += row.KeyPressCount;
                clicks[row.Hour] += row.MouseClickCount;
                wheels[row.Hour] += row.WheelEventCount;
            }
        }

        foreach (var pair in unflushed.HourlyCounts)
        {
            if (pair.Key.Date != date || pair.Key.Hour is < 0 or > 23)
            {
                continue;
            }

            keys[pair.Key.Hour] += pair.Value.KeyPressCount;
            clicks[pair.Key.Hour] += pair.Value.MouseClickCount;
            wheels[pair.Key.Hour] += pair.Value.WheelEventCount;
        }

        var points = new HourlyPoint[24];
        for (var hour = 0; hour < 24; hour++)
        {
            points[hour] = new HourlyPoint(hour, keys[hour], clicks[hour], wheels[hour]);
        }

        return points;
    }

    public async Task<DashboardInsights> GetInsightsAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var hours = await GetTodayHourlyAsync(date, cancellationToken).ConfigureAwait(false);
        var peak = hours.OrderByDescending(point => point.ActivityCount).ThenBy(point => point.Hour).FirstOrDefault();
        var unflushed = _reader.CaptureUnflushed();

        var shortcuts = (await ReadAsync(
            () => _repository.GetShortcutStatsAsync(date, date, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false))
            .ToDictionary(row => row.ShortcutCode, row => row.PressCount, StringComparer.Ordinal);
        if (unflushed.ShortcutCountsByDate.TryGetValue(date, out var liveShortcuts))
            foreach (var pair in liveShortcuts) shortcuts[pair.Key] = shortcuts.GetValueOrDefault(pair.Key) + pair.Value;
        var topShortcut = shortcuts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).FirstOrDefault();

        var apps = (await ReadAsync(
            () => _repository.GetAppStatsAsync(date, date, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false))
            .GroupBy(row => row.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.ActiveSeconds), StringComparer.OrdinalIgnoreCase);
        if (unflushed.AppStatsByDate.TryGetValue(date, out var liveApps))
            foreach (var pair in liveApps) apps[pair.Key] = apps.GetValueOrDefault(pair.Key) + pair.Value.ActiveSeconds;
        var topApp = apps.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

        var earliest = await _repository.GetEarliestStatDateAsync(cancellationToken).ConfigureAwait(false);
        var start = earliest is null || earliest > date ? date : earliest.Value;
        var dailyKeys = await ReadAsync(
            () => _repository.GetKeyStatsAsync(start, date, cancellationToken).GetAwaiter().GetResult(), cancellationToken).ConfigureAwait(false);
        var dailyMouse = await ReadAsync(
            () => _repository.GetMouseStatsAsync(start, date, cancellationToken).GetAwaiter().GetResult(), cancellationToken).ConfigureAwait(false);
        var totals = new Dictionary<DateOnly, long>();
        foreach (var row in dailyKeys) totals[row.Date] = totals.GetValueOrDefault(row.Date) + row.PressCount;
        foreach (var row in dailyMouse) totals[row.Date] = totals.GetValueOrDefault(row.Date) + Clicks(row.Mouse) + Wheel(row.Mouse);
        foreach (var pair in unflushed.KeyCountsByDate) totals[pair.Key] = totals.GetValueOrDefault(pair.Key) + pair.Value.Values.Sum();
        foreach (var pair in unflushed.MouseByDate) totals[pair.Key] = totals.GetValueOrDefault(pair.Key) + Clicks(pair.Value) + Wheel(pair.Value);
        var todayActivity = totals.GetValueOrDefault(date);
        var previous = totals.Where(pair => pair.Key < date).Select(pair => pair.Value).ToArray();
        var record = totals.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).FirstOrDefault();

        return new DashboardInsights(
            peak is { ActivityCount: > 0 } ? peak.Hour : null,
            peak?.ActivityCount ?? 0,
            string.IsNullOrEmpty(topShortcut.Key) ? null : topShortcut.Key,
            topShortcut.Value,
            string.IsNullOrEmpty(topApp.Key) ? null : topApp.Key,
            todayActivity,
            previous.Length == 0 ? 0 : previous.Average(),
            record.Value > 0 ? record.Key : null,
            record.Value);
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    private static long Clicks(in MouseTotals mouse) =>
        mouse.Left + mouse.Right + mouse.Middle + mouse.XButton1 + mouse.XButton2;

    private static long Wheel(in MouseTotals mouse) =>
        mouse.WheelUp + mouse.WheelDown + mouse.WheelLeft + mouse.WheelRight;

    private static Task<T> ReadAsync<T>(Func<T> read, CancellationToken cancellationToken) =>
        Task.Run(read, cancellationToken);
}
