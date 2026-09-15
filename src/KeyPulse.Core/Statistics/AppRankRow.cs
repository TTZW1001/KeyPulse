namespace KeyPulse.Core.Statistics;

public sealed record AppRankRow(
    string ProcessName,
    string? DisplayName,
    long KeyPressCount,
    long MouseClickCount,
    long WheelEventCount,
    double MouseDistancePixels,
    long ActiveSeconds)
{
    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(DisplayName) ? ProcessName : DisplayName;
}
