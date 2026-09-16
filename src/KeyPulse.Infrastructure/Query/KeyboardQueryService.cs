using KeyPulse.Core.Interfaces;
using KeyPulse.Infrastructure.Persistence;

namespace KeyPulse.Infrastructure.Query;

public sealed class KeyboardQueryService : IKeyboardQuery
{
    private readonly IStatisticsRepository _repository;
    private readonly IStatisticsReader _reader;

    public KeyboardQueryService(IStatisticsRepository repository, IStatisticsReader reader)
    {
        _repository = repository;
        _reader = reader;
    }

    public async Task<IReadOnlyDictionary<string, long>> GetKeyCountsAsync(
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
            () => _repository.GetKeyStatsAsync(from, to, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken).ConfigureAwait(false);
        var unflushed = _reader.CaptureUnflushed();

        var keys = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var row in persisted)
        {
            if (row.Date < from || row.Date > to)
            {
                continue;
            }

            keys[row.KeyCode] = keys.GetValueOrDefault(row.KeyCode) + row.PressCount;
        }

        foreach (var day in unflushed.KeyCountsByDate)
        {
            if (day.Key < from || day.Key > to)
            {
                continue;
            }

            foreach (var pair in day.Value)
            {
                keys[pair.Key] = keys.GetValueOrDefault(pair.Key) + pair.Value;
            }
        }

        return keys;
    }

    public async Task<IReadOnlyDictionary<string, long>> GetShortcutCountsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var persisted = await _repository.GetShortcutStatsAsync(from, to, cancellationToken)
            .ConfigureAwait(false);
        var unflushed = _reader.CaptureUnflushed();
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var row in persisted)
        {
            counts[row.ShortcutCode] = counts.GetValueOrDefault(row.ShortcutCode) + row.PressCount;
        }

        foreach (var day in unflushed.ShortcutCountsByDate)
        {
            if (day.Key < from || day.Key > to) continue;
            foreach (var pair in day.Value)
            {
                counts[pair.Key] = counts.GetValueOrDefault(pair.Key) + pair.Value;
            }
        }

        return counts;
    }
}
