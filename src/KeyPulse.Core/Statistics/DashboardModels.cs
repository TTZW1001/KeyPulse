namespace KeyPulse.Core.Statistics;

public sealed record DashboardToday(
    DateOnly Date,
    long KeyPressCount,
    long MouseClickCount,
    long WheelEventCount,
    double DistancePixels,
    string? TopKey,
    long TopKeyCount,
    TrackingState State);

public sealed record DailyTrendPoint(
    DateOnly Date,
    long KeyPressCount,
    long MouseClickCount,
    long WheelEventCount);

public sealed record HourlyPoint(int Hour, long ActivityCount);
