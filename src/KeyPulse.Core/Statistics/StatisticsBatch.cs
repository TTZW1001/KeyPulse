namespace KeyPulse.Core.Statistics;

public sealed record StatisticsBatch(
    IReadOnlyDictionary<DateOnly, IReadOnlyDictionary<string, long>> KeyCountsByDate,
    IReadOnlyDictionary<DateOnly, IReadOnlyDictionary<string, long>> ShortcutCountsByDate,
    IReadOnlyDictionary<DateOnly, MouseTotals> MouseByDate,
    IReadOnlyDictionary<HourBucket, HourlyActivity> HourlyCounts,
    IReadOnlyDictionary<string, long> AppCounts,
    IReadOnlyDictionary<DateOnly, IReadOnlyDictionary<string, AppDayTotals>> AppStatsByDate,
    DateTimeOffset? LastInputTime,
    PointerStatistics? Pointer = null)
{
    public StatisticsBatch(
        IReadOnlyDictionary<DateOnly, IReadOnlyDictionary<string, long>> keyCountsByDate,
        IReadOnlyDictionary<DateOnly, MouseTotals> mouseByDate,
        IReadOnlyDictionary<HourBucket, HourlyActivity> hourlyCounts,
        IReadOnlyDictionary<string, long> appCounts,
        IReadOnlyDictionary<DateOnly, IReadOnlyDictionary<string, AppDayTotals>> appStatsByDate,
        DateTimeOffset? lastInputTime)
        : this(
            keyCountsByDate,
            new Dictionary<DateOnly, IReadOnlyDictionary<string, long>>(),
            mouseByDate,
            hourlyCounts,
            appCounts,
            appStatsByDate,
            lastInputTime)
    {
    }

    public bool IsEmpty =>
        KeyCountsByDate.Count == 0 &&
        ShortcutCountsByDate.Count == 0 &&
        MouseByDate.Count == 0 &&
        HourlyCounts.Count == 0 &&
        AppStatsByDate.Count == 0 &&
        (Pointer is null || Pointer.IsEmpty);
}
