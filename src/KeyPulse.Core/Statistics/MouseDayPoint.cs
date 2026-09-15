namespace KeyPulse.Core.Statistics;

public sealed record MouseDayPoint(
    DateOnly Date,
    long ClickCount,
    long WheelCount,
    double DistancePixels);
