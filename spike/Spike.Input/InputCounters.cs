namespace Spike.Input;

internal sealed class InputCounters
{
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _keys = new(StringComparer.Ordinal);

    public long Left;
    public long Right;
    public long Middle;
    public long X1;
    public long X2;
    public long WheelUp;
    public long WheelDown;
    public long WheelLeft;
    public long WheelRight;
    public double DistancePixels;
    public long MoveEvents;
    public long KeyDownEvents;
    public long WmInputMessages;
    public long UiRefreshTicks;
    public bool Paused;

    public void AddKey(string key)
    {
        lock (_gate)
        {
            if (Paused)
            {
                return;
            }

            KeyDownEvents++;
            _keys[key] = _keys.GetValueOrDefault(key) + 1;
        }
    }

    public void AddMouseButton(string button)
    {
        lock (_gate)
        {
            if (Paused)
            {
                return;
            }

            switch (button)
            {
                case "Left": Left++; break;
                case "Right": Right++; break;
                case "Middle": Middle++; break;
                case "X1": X1++; break;
                case "X2": X2++; break;
            }
        }
    }

    public void AddWheel(int verticalDelta, int horizontalDelta)
    {
        lock (_gate)
        {
            if (Paused)
            {
                return;
            }

            if (verticalDelta > 0) WheelUp++;
            else if (verticalDelta < 0) WheelDown++;

            if (horizontalDelta > 0) WheelRight++;
            else if (horizontalDelta < 0) WheelLeft++;
        }
    }

    public void AddDistance(double pixels)
    {
        lock (_gate)
        {
            if (Paused)
            {
                return;
            }

            DistancePixels += pixels;
            MoveEvents++;
        }
    }

    public void IncrementWmInput()
    {
        Interlocked.Increment(ref WmInputMessages);
    }

    public Snapshot Capture()
    {
        lock (_gate)
        {
            return new Snapshot(
                new Dictionary<string, long>(_keys, StringComparer.Ordinal),
                Left,
                Right,
                Middle,
                X1,
                X2,
                WheelUp,
                WheelDown,
                WheelLeft,
                WheelRight,
                DistancePixels,
                MoveEvents,
                KeyDownEvents,
                Interlocked.Read(ref WmInputMessages),
                UiRefreshTicks,
                Paused);
        }
    }

    internal readonly record struct Snapshot(
        IReadOnlyDictionary<string, long> Keys,
        long Left,
        long Right,
        long Middle,
        long X1,
        long X2,
        long WheelUp,
        long WheelDown,
        long WheelLeft,
        long WheelRight,
        double DistancePixels,
        long MoveEvents,
        long KeyDownEvents,
        long WmInputMessages,
        long UiRefreshTicks,
        bool Paused);
}
