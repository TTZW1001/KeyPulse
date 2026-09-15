namespace KeyPulse.Core.Statistics;

public sealed record StatisticsBatch(
    IReadOnlyDictionary<DateOnly, IReadOnlyDictionary<string, long>> KeyCountsByDate,
    IReadOnlyDictionary<DateOnly, MouseTotals> MouseByDate,
    IReadOnlyDictionary<HourBucket, HourlyActivity> HourlyCounts,
    IReadOnlyDictionary<string, long> AppCounts,
    DateTimeOffset? LastInputTime);
