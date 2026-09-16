namespace KeyPulse.Core.Statistics;

public readonly record struct MouseTotals(
    long Left,
    long Right,
    long Middle,
    long XButton1,
    long XButton2,
    long WheelUp,
    long WheelDown,
    long WheelLeft,
    long WheelRight,
    double DistancePixels,
    double CursorDistancePixels = 0,
    double EstimatedDistanceMeters = 0)
{
    public static MouseTotals Zero { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public MouseTotals Add(in MouseTotals other) => new(
        Left + other.Left,
        Right + other.Right,
        Middle + other.Middle,
        XButton1 + other.XButton1,
        XButton2 + other.XButton2,
        WheelUp + other.WheelUp,
        WheelDown + other.WheelDown,
        WheelLeft + other.WheelLeft,
        WheelRight + other.WheelRight,
        DistancePixels + other.DistancePixels,
        CursorDistancePixels + other.CursorDistancePixels,
        EstimatedDistanceMeters + other.EstimatedDistanceMeters);
}
