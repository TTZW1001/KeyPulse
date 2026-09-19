namespace KeyPulse.Core.Statistics;

public sealed record DashboardToday(
    DateOnly Date,
    long KeyPressCount,
    long MouseClickCount,
    long WheelEventCount,
    double DistancePixels,
    string? TopKey,
    long TopKeyCount,
    TrackingState State,
    double EstimatedDistanceMeters = 0,
    long EffectiveActiveSeconds = 0,
    int ActivitySessionCount = 0,
    bool HasEffectiveTime = false);

public sealed record DailyTrendPoint(
    DateOnly Date,
    long KeyPressCount,
    long MouseClickCount,
    long WheelEventCount,
    long EffectiveActiveSeconds = 0,
    bool HasEffectiveTime = false);

public sealed record HourlyPoint(
    int Hour,
    long KeyPressCount,
    long MouseClickCount,
    long WheelEventCount,
    long EffectiveActiveSeconds = 0,
    bool HasEffectiveTime = false)
{
    public HourlyPoint(int hour, long activityCount)
        : this(hour, activityCount, 0, 0)
    {
    }

    public long ActivityCount => KeyPressCount + MouseClickCount + WheelEventCount;
}

public sealed record DashboardInsights(
    int? PeakHour,
    long PeakActivity,
    string? TopShortcut,
    long TopShortcutCount,
    string? TopApp,
    long TodayActivity,
    double PreviousDailyAverage,
    DateOnly? RecordDate,
    long RecordActivity);
