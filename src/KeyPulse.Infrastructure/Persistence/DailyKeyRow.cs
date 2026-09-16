using KeyPulse.Core.Statistics;

namespace KeyPulse.Infrastructure.Persistence;

public sealed record DailyKeyRow(DateOnly Date, string KeyCode, long PressCount);

public sealed record DailyShortcutRow(DateOnly Date, string ShortcutCode, long PressCount);

public sealed record DailyMouseRow(DateOnly Date, MouseTotals Mouse);

public sealed record HourlyRow(
    DateOnly Date,
    int Hour,
    long KeyPressCount,
    long MouseClickCount,
    long WheelEventCount,
    double MouseDistancePixels,
    long ActiveSeconds,
    double CursorDistancePixels = 0,
    double EstimatedDistanceMeters = 0);

public sealed record DashboardSummary(
    DateOnly Date,
    long KeyPressCount,
    long MouseClickCount,
    double MouseDistancePixels);

public sealed record DailyAppRow(
    DateOnly Date,
    string ProcessName,
    string? DisplayName,
    long KeyPressCount,
    long MouseClickCount,
    long WheelEventCount,
    double MouseDistancePixels,
    long ActiveSeconds,
    double CursorDistancePixels = 0,
    double EstimatedDistanceMeters = 0);
