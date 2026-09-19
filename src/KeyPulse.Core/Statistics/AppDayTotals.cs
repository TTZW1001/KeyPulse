namespace KeyPulse.Core.Statistics;

public readonly record struct AppDayTotals(
    long KeyPressCount,
    long MouseClickCount,
    long WheelEventCount,
    double MouseDistancePixels,
    long ActiveSeconds,
    string? DisplayName,
    double CursorDistancePixels = 0,
    double EstimatedDistanceMeters = 0,
    long EffectiveActiveSeconds = 0)
{
    public static AppDayTotals Zero { get; } = new(0, 0, 0, 0, 0, null);

    public AppDayTotals Add(in AppDayTotals other) => new(
        KeyPressCount + other.KeyPressCount,
        MouseClickCount + other.MouseClickCount,
        WheelEventCount + other.WheelEventCount,
        MouseDistancePixels + other.MouseDistancePixels,
        ActiveSeconds + other.ActiveSeconds,
        FirstNonEmpty(DisplayName, other.DisplayName),
        CursorDistancePixels + other.CursorDistancePixels,
        EstimatedDistanceMeters + other.EstimatedDistanceMeters,
        EffectiveActiveSeconds + other.EffectiveActiveSeconds);

    public long ActivityCount => KeyPressCount + MouseClickCount + WheelEventCount;

    private static string? FirstNonEmpty(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) ? left : right;
}
