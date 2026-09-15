namespace KeyPulse.Core.Statistics;

public sealed record StatisticsSnapshot(
    TrackingState State,
    IReadOnlyDictionary<string, long> KeyCounts,
    MouseTotals Mouse,
    IReadOnlyDictionary<HourBucket, HourlyActivity> HourlyCounts,
    IReadOnlyDictionary<string, long> AppCounts,
    DateTimeOffset? LastInputTime);
