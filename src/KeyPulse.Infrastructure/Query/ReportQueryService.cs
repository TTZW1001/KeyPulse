using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Persistence;

namespace KeyPulse.Infrastructure.Query;

public sealed class ReportQueryService : IReportQuery
{
    private readonly IStatisticsRepository _repository;

    public ReportQueryService(IStatisticsRepository repository) => _repository = repository;

    public async Task<ActivityReport> GetAsync(
        ReportPeriodKind period,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = CurrentRange(period, today);
        var (comparisonFrom, comparisonTo) = ComparisonRange(period, from, to);
        var allFrom = comparisonFrom;

        var keys = await _repository.GetKeyStatsAsync(allFrom, to, cancellationToken).ConfigureAwait(false);
        var mouse = await _repository.GetMouseStatsAsync(allFrom, to, cancellationToken).ConfigureAwait(false);
        var hourly = await _repository.GetHourlyAsync(allFrom, to, cancellationToken).ConfigureAwait(false);
        var sessions = await _repository.GetActivitySessionsAsync(allFrom, to, cancellationToken).ConfigureAwait(false);
        var apps = await _repository.GetAppStatsAsync(from, to, cancellationToken).ConfigureAwait(false);
        var current = Totals(from, to, keys, mouse, hourly, sessions);
        var comparison = Totals(comparisonFrom, comparisonTo, keys, mouse, hourly, sessions);
        var topKey = keys.Where(row => row.Date >= from && row.Date <= to)
            .GroupBy(row => row.KeyCode, StringComparer.Ordinal)
            .Select(group => new { Name = group.Key, Count = group.Sum(row => row.PressCount) })
            .OrderByDescending(item => item.Count).ThenBy(item => item.Name, StringComparer.Ordinal).FirstOrDefault();
        var topApp = apps.GroupBy(row => row.DisplayName ?? row.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new { Name = group.Key, Seconds = group.Sum(row => row.EffectiveActiveSeconds) })
            .Where(item => item.Seconds > 0)
            .OrderByDescending(item => item.Seconds).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        var series = BuildSeries(period, from, to, keys, mouse, hourly);
        var trackingStart = await _repository.GetMetaAsync("effective_tracking_started_at", cancellationToken).ConfigureAwait(false);
        var hasEffective = DateOnly.TryParse(trackingStart, out var start) && to >= start;

        return new ActivityReport(period, from, to, comparisonFrom, comparisonTo, current, comparison,
            topKey?.Name, topKey?.Count ?? 0, topApp?.Name, topApp?.Seconds ?? 0, series, hasEffective);
    }

    private static (DateOnly From, DateOnly To) CurrentRange(ReportPeriodKind period, DateOnly today)
    {
        if (period == ReportPeriodKind.CurrentMonth) return (new DateOnly(today.Year, today.Month, 1), today);
        if (period == ReportPeriodKind.Last12Months)
        {
            var month = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);
            return (month, today);
        }
        var offset = ((int)today.DayOfWeek + 6) % 7;
        return (today.AddDays(-offset), today);
    }

    private static (DateOnly From, DateOnly To) ComparisonRange(
        ReportPeriodKind period, DateOnly from, DateOnly to)
    {
        if (period == ReportPeriodKind.CurrentWeek)
            return (from.AddDays(-7), to.AddDays(-7));
        if (period == ReportPeriodKind.CurrentMonth)
        {
            var previousStart = from.AddMonths(-1);
            var previousLast = previousStart.AddMonths(1).AddDays(-1);
            return (previousStart, previousStart.AddDays(Math.Min(to.Day - 1, previousLast.Day - 1)));
        }
        var days = to.DayNumber - from.DayNumber + 1;
        var comparisonTo = from.AddDays(-1);
        return (comparisonTo.AddDays(-(days - 1)), comparisonTo);
    }

    private static ReportTotals Totals(
        DateOnly from, DateOnly to,
        IReadOnlyList<DailyKeyRow> keys,
        IReadOnlyList<DailyMouseRow> mouse,
        IReadOnlyList<HourlyRow> hourly,
        IReadOnlyList<ActivitySession> sessions)
    {
        var selectedMouse = mouse.Where(row => row.Date >= from && row.Date <= to).Select(row => row.Mouse).ToArray();
        return new ReportTotals(
            keys.Where(row => row.Date >= from && row.Date <= to).Sum(row => row.PressCount),
            selectedMouse.Sum(Clicks), selectedMouse.Sum(Wheels),
            hourly.Where(row => row.Date >= from && row.Date <= to).Sum(row => row.ActiveSeconds),
            sessions.Where(row => row.Date >= from && row.Date <= to).Select(row => row.SessionId).Distinct().Count(),
            selectedMouse.Sum(row => row.CursorDistancePixels));
    }

    private static IReadOnlyList<ReportSeriesPoint> BuildSeries(
        ReportPeriodKind period, DateOnly from, DateOnly to,
        IReadOnlyList<DailyKeyRow> keys, IReadOnlyList<DailyMouseRow> mouse, IReadOnlyList<HourlyRow> hourly)
    {
        var rows = new List<ReportSeriesPoint>();
        if (period == ReportPeriodKind.Last12Months)
        {
            for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
            {
                var end = month.AddMonths(1).AddDays(-1);
                if (end > to) end = to;
                rows.Add(Point(month, month.ToString("yyyy/MM"), month, end, keys, mouse, hourly));
            }
        }
        else
        {
            for (var date = from; date <= to; date = date.AddDays(1))
                rows.Add(Point(date, date.ToString("M/d"), date, date, keys, mouse, hourly));
        }
        return rows;
    }

    private static ReportSeriesPoint Point(
        DateOnly date, string label, DateOnly from, DateOnly to,
        IReadOnlyList<DailyKeyRow> keys, IReadOnlyList<DailyMouseRow> mouse, IReadOnlyList<HourlyRow> hourly)
    {
        var keyCount = keys.Where(row => row.Date >= from && row.Date <= to).Sum(row => row.PressCount);
        var selectedMouse = mouse.Where(row => row.Date >= from && row.Date <= to).Select(row => row.Mouse).ToArray();
        var activity = keyCount + selectedMouse.Sum(Clicks) + selectedMouse.Sum(Wheels);
        var seconds = hourly.Where(row => row.Date >= from && row.Date <= to).Sum(row => row.ActiveSeconds);
        return new ReportSeriesPoint(date, label, activity, seconds);
    }

    private static long Clicks(MouseTotals mouse) => mouse.Left + mouse.Right + mouse.Middle + mouse.XButton1 + mouse.XButton2;
    private static long Wheels(MouseTotals mouse) => mouse.WheelUp + mouse.WheelDown + mouse.WheelLeft + mouse.WheelRight;
}
