namespace KeyPulse.Core.Models;

public sealed record DisplayMonitor(
    string Id,
    int Left,
    int Top,
    int Width,
    int Height,
    double DpiX,
    double DpiY,
    bool IsPrimary);

public sealed record DisplayLayout(
    string Signature,
    int VirtualLeft,
    int VirtualTop,
    int VirtualWidth,
    int VirtualHeight,
    IReadOnlyList<DisplayMonitor> Monitors);

public readonly record struct PointerPosition(
    int X,
    int Y,
    DisplayLayout Layout,
    DisplayMonitor Monitor)
{
    public int MonitorX => X - Monitor.Left;

    public int MonitorY => Y - Monitor.Top;
}
