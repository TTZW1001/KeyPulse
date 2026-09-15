using KeyPulse.Core.Statistics;

namespace KeyPulse.Infrastructure.Persistence;

public sealed record DailyKeyRow(DateOnly Date, string KeyCode, long PressCount);

public sealed record DailyMouseRow(DateOnly Date, MouseTotals Mouse);

public sealed record HourlyRow(
    DateOnly Date,
    int Hour,
    long KeyPressCount,
    long MouseClickCount,
    long WheelEventCount,
    double MouseDistancePixels,
    long ActiveSeconds);

public sealed record DashboardSummary(
    DateOnly Date,
    long KeyPressCount,
    long MouseClickCount,
    double MouseDistancePixels);
