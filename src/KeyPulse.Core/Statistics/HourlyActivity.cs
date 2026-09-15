namespace KeyPulse.Core.Statistics;

public readonly record struct HourlyActivity(
    long KeyPressCount,
    long MouseClickCount,
    long WheelEventCount,
    double MouseDistancePixels);
