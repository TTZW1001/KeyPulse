using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Persistence;

namespace KeyPulse.Infrastructure.Query;

public sealed class TrendQueryService : ITrendQuery
{
    private readonly IStatisticsRepository _repository;
    private readonly IStatisticsReader _reader;

    public TrendQueryService(IStatisticsRepository repository, IStatisticsReader reader)
    {
        _repository = repository;
        _reader = reader;
    }

    public async Task<DateOnly?> GetEarliestDateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var persisted = await Task.Run(
            () => _repository.GetEarliestStatDateAsync(cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false);
        var unflushed = _reader.CaptureUnflushed();
        DateOnly? earliest = persisted;
        foreach (var date in unflushed.KeyCountsByDate.Keys)
        {
            earliest = Min(earliest, date);
        }

        foreach (var date in unflushed.MouseByDate.Keys)
        {
            earliest = Min(earliest, date);
        }

        foreach (var bucket in unflushed.HourlyCounts.Keys)
        {
            earliest = Min(earliest, bucket.Date);
        }

        return earliest;
    }

    public async Task<IReadOnlyList<DailyTrendPoint>> GetDailyAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (from > to)
        {
            (from, to) = (to, from);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var persistedKeys = await Task.Run(
            () => _repository.GetKeyStatsAsync(from, to, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false);
        var persistedMouse = await Task.Run(
            () => _repository.GetMouseStatsAsync(from, to, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false);
        var unflushed = _reader.CaptureUnflushed();

        var days = to.DayNumber - from.DayNumber + 1;
        var points = new DailyTrendPoint[days];
        for (var i = 0; i < days; i++)
        {
            var date = from.AddDays(i);
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

    public async Task<IReadOnlyList<HourlyPoint>> GetHourlyAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (from > to)
        {
            (from, to) = (to, from);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var persisted = await Task.Run(
            () => _repository.GetHourlyAsync(from, to, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false);
        var unflushed = _reader.CaptureUnflushed();

        var hours = new long[24];
        foreach (var row in persisted)
        {
            if (row.Date < from || row.Date > to || row.Hour is < 0 or > 23)
            {
                continue;
            }

            hours[row.Hour] += row.KeyPressCount + row.MouseClickCount + row.WheelEventCount;
        }

        foreach (var pair in unflushed.HourlyCounts)
        {
            if (pair.Key.Date < from || pair.Key.Date > to || pair.Key.Hour is < 0 or > 23)
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

    private static DateOnly? Min(DateOnly? current, DateOnly candidate) =>
        current is null || candidate < current.Value ? candidate : current;

    private static long Clicks(in MouseTotals mouse) =>
        mouse.Left + mouse.Right + mouse.Middle + mouse.XButton1 + mouse.XButton2;

    private static long Wheel(in MouseTotals mouse) =>
        mouse.WheelUp + mouse.WheelDown + mouse.WheelLeft + mouse.WheelRight;
}
