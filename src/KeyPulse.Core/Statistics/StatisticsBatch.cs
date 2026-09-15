namespace KeyPulse.Core.Statistics;

public sealed record StatisticsBatch(
    IReadOnlyDictionary<DateOnly, IReadOnlyDictionary<string, long>> KeyCountsByDate,
    IReadOnlyDictionary<DateOnly, MouseTotals> MouseByDate,
    IReadOnlyDictionary<HourBucket, HourlyActivity> HourlyCounts,
    IReadOnlyDictionary<string, long> AppCounts,
    IReadOnlyDictionary<DateOnly, IReadOnlyDictionary<string, AppDayTotals>> AppStatsByDate,
    DateTimeOffset? LastInputTime)
{
    public bool IsEmpty =>
        KeyCountsByDate.Count == 0 &&
        MouseByDate.Count == 0 &&
        HourlyCounts.Count == 0 &&
        AppStatsByDate.Count == 0;
}
