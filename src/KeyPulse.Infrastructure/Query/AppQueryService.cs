using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Persistence;

namespace KeyPulse.Infrastructure.Query;

public sealed class AppQueryService : IAppQuery
{
    private readonly IStatisticsRepository _repository;
    private readonly IStatisticsReader _reader;

    public AppQueryService(IStatisticsRepository repository, IStatisticsReader reader)
    {
        _repository = repository;
        _reader = reader;
    }

    public async Task<IReadOnlyList<AppRankRow>> GetRankingAsync(
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
            () => _repository.GetAppStatsAsync(from, to, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false);
        var unflushed = _reader.CaptureUnflushed();

        var map = new Dictionary<string, Mutable>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in persisted)
        {
            if (row.Date < from || row.Date > to)
            {
                continue;
            }

            Add(map, row.ProcessName, row.DisplayName, row.KeyPressCount, row.MouseClickCount,
                row.WheelEventCount, row.MouseDistancePixels, row.ActiveSeconds);
        }

        foreach (var day in unflushed.AppStatsByDate)
        {
            if (day.Key < from || day.Key > to)
            {
                continue;
            }

            foreach (var app in day.Value)
            {
                Add(map, app.Key, app.Value.DisplayName, app.Value.KeyPressCount, app.Value.MouseClickCount,
                    app.Value.WheelEventCount, app.Value.MouseDistancePixels, app.Value.ActiveSeconds);
            }
        }

        return map.Values
            .Select(item => item.ToRow())
            .OrderByDescending(row => row.KeyPressCount)
            .ThenBy(row => row.DisplayLabel, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void Add(
        Dictionary<string, Mutable> map,
        string processName,
        string? displayName,
        long keys,
        long clicks,
        long wheels,
        double distance,
        long active)
    {
        if (!map.TryGetValue(processName, out var item))
        {
            item = new Mutable { ProcessName = processName };
            map[processName] = item;
        }

        item.KeyPressCount += keys;
        item.MouseClickCount += clicks;
        item.WheelEventCount += wheels;
        item.MouseDistancePixels += distance;
        item.ActiveSeconds += active;
        if (string.IsNullOrWhiteSpace(item.DisplayName) && !string.IsNullOrWhiteSpace(displayName))
        {
            item.DisplayName = displayName;
        }
    }

    private sealed class Mutable
    {
        public string ProcessName = string.Empty;
        public string? DisplayName;
        public long KeyPressCount;
        public long MouseClickCount;
        public long WheelEventCount;
        public double MouseDistancePixels;
        public long ActiveSeconds;

        public AppRankRow ToRow() => new(
            ProcessName, DisplayName, KeyPressCount, MouseClickCount,
            WheelEventCount, MouseDistancePixels, ActiveSeconds);
    }
}
