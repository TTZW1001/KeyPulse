using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Persistence;

namespace KeyPulse.Infrastructure.Query;

public sealed class MouseQueryService : IMouseQuery
{
    private readonly IStatisticsRepository _repository;
    private readonly IStatisticsReader _reader;

    public MouseQueryService(IStatisticsRepository repository, IStatisticsReader reader)
    {
        _repository = repository;
        _reader = reader;
    }

    public async Task<MouseTotals> GetMouseTotalsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var persisted = await Task.Run(
            () => _repository.GetMouseStatsAsync(from, to, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false);
        var unflushed = _reader.CaptureUnflushed();

        var total = MouseTotals.Zero;
        foreach (var row in persisted)
        {
            if (row.Date < from || row.Date > to)
            {
                continue;
            }

            total = total.Add(row.Mouse);
        }

        foreach (var day in unflushed.MouseByDate)
        {
            if (day.Key < from || day.Key > to)
            {
                continue;
            }

            total = total.Add(day.Value);
        }

        return total;
    }

    public async Task<IReadOnlyList<MouseDayPoint>> GetLast7DaysAsync(
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = endDate.AddDays(-6);
        var persisted = await Task.Run(
            () => _repository.GetMouseStatsAsync(start, endDate, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false);
        var unflushed = _reader.CaptureUnflushed();

        var points = new MouseDayPoint[7];
        for (var i = 0; i < 7; i++)
        {
            var date = start.AddDays(i);
            var mouse = MouseTotals.Zero;
            foreach (var row in persisted.Where(row => row.Date == date))
            {
                mouse = mouse.Add(row.Mouse);
            }

            if (unflushed.MouseByDate.TryGetValue(date, out var snap))
            {
                mouse = mouse.Add(snap);
            }

            points[i] = new MouseDayPoint(date, Clicks(mouse), Wheel(mouse), mouse.CursorDistancePixels);
        }

        return points;
    }

    private static long Clicks(in MouseTotals mouse) =>
        mouse.Left + mouse.Right + mouse.Middle + mouse.XButton1 + mouse.XButton2;

    private static long Wheel(in MouseTotals mouse) =>
        mouse.WheelUp + mouse.WheelDown + mouse.WheelLeft + mouse.WheelRight;
}
