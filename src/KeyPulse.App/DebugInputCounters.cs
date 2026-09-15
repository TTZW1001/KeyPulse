using KeyPulse.Core.Events;
using KeyPulse.Core.Models;

namespace KeyPulse.App;

public sealed class DebugInputCounters
{
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _keys = new(StringComparer.Ordinal);

    public bool Paused { get; set; }

    public void Add(InputEvent inputEvent)
    {
        if (Paused)
        {
            return;
        }

        lock (_gate)
        {
            switch (inputEvent)
            {
                case KeyPressedEvent key:
                    _keys[key.Key.Name] = _keys.GetValueOrDefault(key.Key.Name) + 1;
                    break;
                case MouseButtonEvent button:
                    switch (button.Button)
                    {
                        case MouseButton.Left: Left++; break;
                        case MouseButton.Right: Right++; break;
                        case MouseButton.Middle: Middle++; break;
                        case MouseButton.XButton1: XButton1++; break;
                        case MouseButton.XButton2: XButton2++; break;
                    }

                    break;
                case MouseWheelEvent wheel when wheel.Horizontal:
                    if (wheel.Delta > 0) WheelRight++;
                    else if (wheel.Delta < 0) WheelLeft++;
                    break;
                case MouseWheelEvent wheel:
                    if (wheel.Delta > 0) WheelUp++;
                    else if (wheel.Delta < 0) WheelDown++;
                    break;
                case MouseMoveEvent move:
                    DistancePixels += Math.Sqrt(
                        ((double)move.DeltaX * move.DeltaX) + ((double)move.DeltaY * move.DeltaY));
                    MoveEvents++;
                    break;
            }
        }
    }

    public long Left { get; private set; }
    public long Right { get; private set; }
    public long Middle { get; private set; }
    public long XButton1 { get; private set; }
    public long XButton2 { get; private set; }
    public long WheelUp { get; private set; }
    public long WheelDown { get; private set; }
    public long WheelLeft { get; private set; }
    public long WheelRight { get; private set; }
    public double DistancePixels { get; private set; }
    public long MoveEvents { get; private set; }

    public Snapshot Capture()
    {
        lock (_gate)
        {
            return new Snapshot(
                new Dictionary<string, long>(_keys, StringComparer.Ordinal),
                Left,
                Right,
                Middle,
                XButton1,
                XButton2,
                WheelUp,
                WheelDown,
                WheelLeft,
                WheelRight,
                DistancePixels,
                MoveEvents,
                Paused);
        }
    }

    public readonly record struct Snapshot(
        IReadOnlyDictionary<string, long> Keys,
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
        long MoveEvents,
        bool Paused);
}
